using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Playback;

/// <summary>把队列和播放器编在一起，对界面暴露一个门面。</summary>
public sealed class PlaybackController
{
    private readonly PlaybackQueue _queue = new();
    private readonly IAudioPlayer _player;
    private readonly Func<int, int> _pickIndex;
    private int _failedAttempts;
    private bool _isTryingCurrent;
    private bool _loadFailed;
    private string _failureMessage = string.Empty;

    /// <param name="pickIndex">随机播放时用来挑索引，参数是上界（不含）。测试里注入可预测的实现。</param>
    public PlaybackController(IAudioPlayer player, Func<int, int>? pickIndex = null)
    {
        _player = player;
        _pickIndex = pickIndex ?? (maxExclusive => Random.Shared.Next(maxExclusive));
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

    /// <summary>播放方式：顺序 / 随机 / 单曲循环。</summary>
    public PlayMode Mode { get; set; } = PlayMode.Sequential;

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
        if (index < 0 || index >= _queue.Tracks.Count)
        {
            return false;
        }

        // 点的就是当前正在播的那一首：什么都不做。
        // 否则一次双击会触发两次 PlayAt，第二次把这首歌从头重新加载。
        if (index == _queue.CurrentIndex && _player.State == PlaybackState.Playing)
        {
            return true;
        }

        if (_queue.SetCurrent(index) is null)
        {
            return false;
        }

        return TryPlayCurrentSkippingBrokenFiles();
    }

    public bool Next() => Mode == PlayMode.Shuffle
        ? MoveShuffled()
        : _queue.MoveNext() && TryPlayCurrentSkippingBrokenFiles();

    public bool Previous() => Mode == PlayMode.Shuffle
        ? MoveShuffled()
        : _queue.MovePrevious() && TryPlayCurrentSkippingBrokenFiles();

    /// <summary>
    /// 随机换一首。从"除当前这首之外"的曲目里挑，所以不会连着播同一首；
    /// 列表里只有一首时只能重播它。
    /// </summary>
    private bool MoveShuffled()
    {
        var count = _queue.Tracks.Count;
        if (count == 0)
        {
            return false;
        }

        if (count == 1)
        {
            return TryPlayCurrentSkippingBrokenFiles();
        }

        var current = _queue.CurrentIndex < 0 ? 0 : _queue.CurrentIndex;
        var offset = 1 + _pickIndex(count - 1);
        var index = (current + offset) % count;

        return _queue.SetCurrent(index) is not null && TryPlayCurrentSkippingBrokenFiles();
    }

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

        // 单曲循环：把当前这首重新放一遍（显式按上一首/下一首不受影响，仍走 Next/Previous）
        if (Mode == PlayMode.RepeatOne && _queue.Current is not null)
        {
            TryPlayCurrentSkippingBrokenFiles();
            return;
        }

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
