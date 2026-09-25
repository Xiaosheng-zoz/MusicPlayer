using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Playback;

/// <summary>把队列和播放器编在一起，对界面暴露一个门面。</summary>
public sealed class PlaybackController
{
    private readonly PlaybackQueue _queue = new();
    private readonly IAudioPlayer _player;
    private readonly ShuffleBag _shuffleBag;
    private int _failedAttempts;
    private PlayMode _mode = PlayMode.Sequential;
    private bool _isTryingCurrent;
    private bool _loadFailed;
    private string _failureMessage = string.Empty;

    /// <param name="pickIndex">随机播放时用来挑索引，参数是上界（不含）。测试里注入可预测的实现。</param>
    public PlaybackController(IAudioPlayer player, Func<int, int>? pickIndex = null)
    {
        _player = player;
        _shuffleBag = new ShuffleBag(pickIndex);
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
    public PlayMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value)
            {
                return;
            }

            _mode = value;
            _shuffleBag.Clear(); // 换模式就重新开一轮，避免沿用上一轮剩下的签
        }
    }

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
        _shuffleBag.Clear();
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

        if (_mode == PlayMode.Shuffle)
        {
            // 手动点的这首也算本轮已播，本轮不会再随机到它
            _shuffleBag.MarkPlayed(index, _queue.Tracks.Count);
        }

        return TryPlayCurrentSkippingBrokenFiles();
    }

    public bool Next()
    {
        if (_mode == PlayMode.Shuffle)
        {
            var index = _shuffleBag.Next(_queue.Tracks.Count, _queue.CurrentIndex);
            return index >= 0
                && _queue.SetCurrent(index) is not null
                && TryPlayCurrentSkippingBrokenFiles();
        }

        // 顺序播放是列表循环：已经是最后一首就回到第一首
        if (_queue.MoveNext())
        {
            return TryPlayCurrentSkippingBrokenFiles();
        }

        return _queue.SetCurrent(0) is not null && TryPlayCurrentSkippingBrokenFiles();
    }

    public bool Previous()
    {
        if (_mode == PlayMode.Shuffle)
        {
            var index = _shuffleBag.Previous(_queue.CurrentIndex);
            if (index < 0 || index == _queue.CurrentIndex)
            {
                return false; // 还没有听过别的歌，没有上一首可退
            }

            return _queue.SetCurrent(index) is not null && TryPlayCurrentSkippingBrokenFiles();
        }

        return _queue.MovePrevious() && TryPlayCurrentSkippingBrokenFiles();
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
