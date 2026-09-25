using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Playback;

/// <summary>播放队列与上/下一首规则。纯逻辑，不碰解码器，可以被完整单测。</summary>
public sealed class PlaybackQueue
{
    private readonly List<Track> _tracks = new();

    public IReadOnlyList<Track> Tracks => _tracks;

    public int CurrentIndex { get; private set; } = -1;

    public bool IsEmpty => _tracks.Count == 0;

    public Track? Current =>
        CurrentIndex >= 0 && CurrentIndex < _tracks.Count ? _tracks[CurrentIndex] : null;

    public void Replace(IEnumerable<Track> tracks)
    {
        _tracks.Clear();
        _tracks.AddRange(tracks);
        CurrentIndex = -1;
    }

    public Track? SetCurrent(int index)
    {
        if (index < 0 || index >= _tracks.Count)
        {
            return null;
        }

        CurrentIndex = index;
        return Current;
    }

    /// <summary>前进一首。已经在末尾时返回 false 且不动。</summary>
    public bool MoveNext()
    {
        if (CurrentIndex + 1 >= _tracks.Count)
        {
            return false;
        }

        CurrentIndex++;
        return true;
    }

    /// <summary>后退一首。已经在开头时返回 false 且不动。</summary>
    public bool MovePrevious()
    {
        if (CurrentIndex - 1 < 0)
        {
            return false;
        }

        CurrentIndex--;
        return true;
    }

    public void Clear()
    {
        _tracks.Clear();
        CurrentIndex = -1;
    }
}
