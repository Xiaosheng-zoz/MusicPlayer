using MusicPlayer.Core.Models;
using MusicPlayer.Core.Playback;

namespace MusicPlayer.Core.Tests;

public class PlaybackQueueTests
{
    private static List<Track> ThreeTracks() => new()
    {
        new Track { FilePath = @"C:\m\1.flac" },
        new Track { FilePath = @"C:\m\2.flac" },
        new Track { FilePath = @"C:\m\3.flac" }
    };

    [Fact]
    public void NewQueue_IsEmpty_AndHasNoCurrent()
    {
        var queue = new PlaybackQueue();

        Assert.True(queue.IsEmpty);
        Assert.Null(queue.Current);
        Assert.Equal(-1, queue.CurrentIndex);
    }

    [Fact]
    public void SetCurrent_ReturnsTrack_AndUpdatesIndex()
    {
        var queue = new PlaybackQueue();
        queue.Replace(ThreeTracks());

        var current = queue.SetCurrent(1);

        Assert.Equal(@"C:\m\2.flac", current!.FilePath);
        Assert.Equal(1, queue.CurrentIndex);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void SetCurrent_ReturnsNull_ForOutOfRangeIndex(int index)
    {
        var queue = new PlaybackQueue();
        queue.Replace(ThreeTracks());

        Assert.Null(queue.SetCurrent(index));
        Assert.Equal(-1, queue.CurrentIndex);
    }

    [Fact]
    public void MoveNext_AdvancesAndReturnsTrue()
    {
        var queue = new PlaybackQueue();
        queue.Replace(ThreeTracks());
        queue.SetCurrent(0);

        Assert.True(queue.MoveNext());
        Assert.Equal(1, queue.CurrentIndex);
    }

    [Fact]
    public void MoveNext_ReturnsFalse_AtEndOfQueue_AndStaysPut()
    {
        var queue = new PlaybackQueue();
        queue.Replace(ThreeTracks());
        queue.SetCurrent(2);

        Assert.False(queue.MoveNext());
        Assert.Equal(2, queue.CurrentIndex);
    }

    [Fact]
    public void MovePrevious_ReturnsFalse_AtStartOfQueue_AndStaysPut()
    {
        var queue = new PlaybackQueue();
        queue.Replace(ThreeTracks());
        queue.SetCurrent(0);

        Assert.False(queue.MovePrevious());
        Assert.Equal(0, queue.CurrentIndex);
    }

    [Fact]
    public void SingleTrackQueue_CannotMoveEitherWay()
    {
        var queue = new PlaybackQueue();
        queue.Replace(new List<Track> { new() { FilePath = @"C:\m\only.flac" } });
        queue.SetCurrent(0);

        Assert.False(queue.MoveNext());
        Assert.False(queue.MovePrevious());
    }

    [Fact]
    public void Replace_ResetsCurrentIndex()
    {
        var queue = new PlaybackQueue();
        queue.Replace(ThreeTracks());
        queue.SetCurrent(2);

        queue.Replace(ThreeTracks());

        Assert.Equal(-1, queue.CurrentIndex);
        Assert.Null(queue.Current);
    }
}
