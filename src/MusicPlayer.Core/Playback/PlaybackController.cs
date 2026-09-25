using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Playback;

/// <summary>把队列和播放器编在一起，对界面暴露一个门面。</summary>
public sealed class PlaybackController
{
    private readonly PlaybackQueue _queue = new();
    private readonly IAudioPlayer _player;
    private int _failedAttempts;
    private bool _isTryingCurrent;
    private bool _loadFailed;
    private string _failureMessage = string.Empty;

    public PlaybackController(IAudioPlayer player)
    {
        _player = player;
        _player.Ended += OnEnded;
        _player.Failed += OnFailed;
        _player.PositionChanged += (_, _) => PositionChanged?.Invoke(this, _player.Position);
        _player.StateChanged += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? StateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;

    /// <summary>队列里的歌全部播不了时触发一次，参数是给用户看的错误信息。</summary>
    public event EventHandler<string>? PlaybackFailed;

    public IReadOnlyList<Track> Tracks => _queue.Tracks;
    public Track? Current => _queue.Current;
    public PlaybackState State => _player.State;
    public TimeSpan Position => _player.Position;
    public TimeSpan Duration => _player.Duration;

    public double Volume
    {
        get => _player.Volume;
        set => _player.Volume = Math.Clamp(value, 0d, 1d);
    }

    public void LoadTracks(IEnumerable<Track> tracks)
    {
        _queue.Replace(tracks);
        _failedAttempts = 0;
    }

    public bool PlayAt(int index)
    {
        if (_queue.SetCurrent(index) is null)
        {
            return false;
        }

        return TryPlayCurrentSkippingBrokenFiles();
    }

    public bool Next() => _queue.MoveNext() && TryPlayCurrentSkippingBrokenFiles();

    public bool Previous() => _queue.MovePrevious() && TryPlayCurrentSkippingBrokenFiles();

    public void TogglePlayPause()
    {
        if (_player.State == PlaybackState.Playing)
        {
            _player.Pause();
        }
        else if (_queue.Current is not null)
        {
            _player.Play();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Seek(TimeSpan position)
    {
        if (_queue.Current is null)
        {
            return;
        }

        var clamped = position < TimeSpan.Zero
            ? TimeSpan.Zero
            : Duration > TimeSpan.Zero && position > Duration ? Duration : position;

        _player.Seek(clamped);
    }

    /// <summary>
    /// 从当前曲目开始播放，遇到播不了的就往下一首走，直到播成功或整个队列都试完。
    /// 必须是循环而不是递归：Load 可能同步回调 OnFailed，递归会在每一层都补一次 Play()，
    /// 既多播一次，也会把已经放弃时设置好的 Stopped 状态又改回 Playing。
    /// </summary>
    private bool TryPlayCurrentSkippingBrokenFiles()
    {
        _isTryingCurrent = true;
        try
        {
            while (_queue.Current is not null)
            {
                _loadFailed = false;
                _player.Load(_queue.Current.FilePath);

                if (!_loadFailed)
                {
                    _player.Play();
                    _failedAttempts = 0;
                    StateChanged?.Invoke(this, EventArgs.Empty);
                    return true;
                }

                _failedAttempts++;

                // 整个队列都试过一遍还是不行，就停下来告诉用户，不要无限重试
                if (_failedAttempts >= _queue.Tracks.Count || !_queue.MoveNext())
                {
                    _player.Stop();
                    PlaybackFailed?.Invoke(this, $"这个列表里的歌都播不了：{_failureMessage}");
                    StateChanged?.Invoke(this, EventArgs.Empty);
                    return false;
                }
            }

            return false;
        }
        finally
        {
            _isTryingCurrent = false;
        }
    }

    private void OnEnded(object? sender, EventArgs e)
    {
        _failedAttempts = 0;

        if (!Next())
        {
            _player.Stop();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnFailed(object? sender, string message)
    {
        _failureMessage = message;

        // Load 期间的同步失败：交给正在跑的 TryPlayCurrentSkippingBrokenFiles 循环处理
        if (_isTryingCurrent)
        {
            _loadFailed = true;
            return;
        }

        // 播放中途才失败（例如文件被删掉）：走同一条跳过逻辑
        _failedAttempts++;
        if (_failedAttempts >= _queue.Tracks.Count || !_queue.MoveNext())
        {
            _player.Stop();
            PlaybackFailed?.Invoke(this, $"这个列表里的歌都播不了：{_failureMessage}");
            StateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        TryPlayCurrentSkippingBrokenFiles();
    }
}
