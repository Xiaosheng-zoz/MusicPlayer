using MusicPlayer.Core.Models;
using MusicPlayer.Core.Playback;
using MusicPlayer.Core.Tests.Fakes;

namespace MusicPlayer.Core.Tests;

public class PlaybackControllerTests
{
    private static List<Track> ThreeTracks() => new()
    {
        new Track { FilePath = @"C:\m\1.flac" },
        new Track { FilePath = @"C:\m\2.flac" },
        new Track { FilePath = @"C:\m\3.flac" }
    };

    [Fact]
    public void PlayAt_LoadsThenPlays()
    {
        var player = new FakeAudioPlayer();
        var controller = new PlaybackController(player);
        controller.LoadTracks(ThreeTracks());

        Assert.True(controller.PlayAt(0));

        Assert.Equal(new[] { @"C:\m\1.flac" }, player.LoadedPaths);
        Assert.Equal(1, player.PlayCount);
        Assert.Equal(PlaybackState.Playing, controller.State);
        Assert.Equal("1", controller.Current!.DisplayTitle);
    }

    [Fact]
    public void PlayAt_ReturnsFalse_ForOutOfRangeIndex()
    {
        var controller = new PlaybackController(new FakeAudioPlayer());
        controller.LoadTracks(ThreeTracks());

        Assert.False(controller.PlayAt(9));
    }

    [Fact]
    public void Ended_AdvancesToNextTrack()
    {
        var player = new FakeAudioPlayer();
        var controller = new PlaybackController(player);
        controller.LoadTracks(ThreeTracks());
        controller.PlayAt(0);

        player.SimulateEnded();

        Assert.Equal(@"C:\m\2.flac", controller.Current!.FilePath);
        Assert.Equal(2, player.PlayCount);
    }

    [Fact]
    public void Ended_OnLastTrack_StopsPlayback()
    {
        var player = new FakeAudioPlayer();
        var controller = new PlaybackController(player);
        controller.LoadTracks(ThreeTracks());
        controller.PlayAt(2);

        player.SimulateEnded();

        Assert.Equal(PlaybackState.Stopped, controller.State);
        Assert.Equal(@"C:\m\3.flac", controller.Current!.FilePath);
    }

    [Fact]
    public void UnplayableFile_SkipsToNextTrack()
    {
        var player = new FakeAudioPlayer();
        player.FailingPaths.Add(@"C:\m\1.flac");
        var controller = new PlaybackController(player);
        controller.LoadTracks(ThreeTracks());

        controller.PlayAt(0);

        Assert.Equal(@"C:\m\2.flac", controller.Current!.FilePath);
        Assert.Equal(1, player.PlayCount);
    }

    [Fact]
    public void AllFilesUnplayable_ReportsFailureOnceAndStops()
    {
        var player = new FakeAudioPlayer();
        foreach (var track in ThreeTracks())
        {
            player.FailingPaths.Add(track.FilePath);
        }

        var controller = new PlaybackController(player);
        var failures = new List<string>();
        controller.PlaybackFailed += (_, message) => failures.Add(message);
        controller.LoadTracks(ThreeTracks());

        controller.PlayAt(0);

        Assert.Single(failures);
        Assert.Equal(PlaybackState.Stopped, controller.State);
        Assert.Equal(3, player.LoadedPaths.Count); // 三首都试过了才放弃
    }

    [Fact]
    public void TogglePlayPause_PausesWhenPlaying_AndResumesWhenPaused()
    {
        var player = new FakeAudioPlayer();
        var controller = new PlaybackController(player);
        controller.LoadTracks(ThreeTracks());
        controller.PlayAt(0);

        controller.TogglePlayPause();
        Assert.Equal(PlaybackState.Paused, controller.State);

        controller.TogglePlayPause();
        Assert.Equal(PlaybackState.Playing, controller.State);
    }

    [Fact]
    public void Volume_IsClampedToZeroAndOne()
    {
        var player = new FakeAudioPlayer();
        var controller = new PlaybackController(player);

        controller.Volume = 1.7;
        Assert.Equal(1.0, player.Volume);

        controller.Volume = -0.4;
        Assert.Equal(0.0, player.Volume);
    }

    [Fact]
    public void Seek_IsClampedToTrackBounds()
    {
        var player = new FakeAudioPlayer { Duration = TimeSpan.FromMinutes(3) };
        var controller = new PlaybackController(player);
        controller.LoadTracks(ThreeTracks());
        controller.PlayAt(0);

        controller.Seek(TimeSpan.FromMinutes(99));
        Assert.Equal(TimeSpan.FromMinutes(3), player.Position);

        controller.Seek(TimeSpan.FromSeconds(-5));
        Assert.Equal(TimeSpan.Zero, player.Position);
    }
}
