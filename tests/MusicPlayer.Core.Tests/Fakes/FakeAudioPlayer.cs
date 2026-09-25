using MusicPlayer.Core.Playback;

namespace MusicPlayer.Core.Tests.Fakes;

/// <summary>可编程的假播放器：记录调用序列，并可以指定哪些路径播放失败。</summary>
internal sealed class FakeAudioPlayer : IAudioPlayer
{
    public List<string> LoadedPaths { get; } = new();
    public HashSet<string> FailingPaths { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int PlayCount { get; private set; }

    public TimeSpan Position { get; private set; }
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(4);
    public double Volume { get; set; } = 0.8;
    public PlaybackState State { get; private set; } = PlaybackState.Stopped;

    public event EventHandler? Ended;
    public event EventHandler? PositionChanged;
    public event EventHandler? StateChanged;
    public event EventHandler<string>? Failed;

    public void Load(string filePath)
    {
        LoadedPaths.Add(filePath);

        if (FailingPaths.Contains(filePath))
        {
            Failed?.Invoke(this, $"无法播放 {Path.GetFileName(filePath)}");
        }
    }

    public void Play()
    {
        PlayCount++;
        State = PlaybackState.Playing;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pause()
    {
        State = PlaybackState.Paused;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        State = PlaybackState.Stopped;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Seek(TimeSpan position)
    {
        Position = position;
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>测试用：模拟一首歌播完。</summary>
    public void SimulateEnded() => Ended?.Invoke(this, EventArgs.Empty);
}
