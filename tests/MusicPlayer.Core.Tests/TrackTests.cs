using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Tests;

public class TrackTests
{
    [Fact]
    public void DisplayTitle_FallsBackToFileNameWithoutExtension_WhenTitleMissing()
    {
        var track = new Track { FilePath = @"C:\music\Aimer - 六等星の夜.flac" };

        Assert.Equal("Aimer - 六等星の夜", track.DisplayTitle);
    }

    [Fact]
    public void DisplayTitle_UsesTagTitle_WhenPresent()
    {
        var track = new Track { FilePath = @"C:\music\track01.flac", Title = "夜航西飞" };

        Assert.Equal("夜航西飞", track.DisplayTitle);
    }

    [Theory]
    [InlineData("", "—")]
    [InlineData("   ", "—")]
    [InlineData("陈粒", "陈粒")]
    public void DisplayArtist_ShowsPlaceholder_WhenMissing(string artist, string expected)
    {
        var track = new Track { FilePath = @"C:\music\a.flac", Artist = artist };

        Assert.Equal(expected, track.DisplayArtist);
    }

    [Fact]
    public void Format_IsUppercaseExtension()
    {
        Assert.Equal("FLAC", new Track { FilePath = @"C:\music\a.FLAC" }.Format);
        Assert.Equal("MP3", new Track { FilePath = @"C:\music\a.mp3" }.Format);
    }

    [Fact]
    public void DisplayDuration_ShowsPlaceholder_WhenDurationUnknown()
    {
        Assert.Equal("--:--", new Track { FilePath = @"C:\music\a.flac" }.DisplayDuration);
    }

    [Fact]
    public void DisplayDuration_FormatsAsMinutesAndSeconds()
    {
        var track = new Track { FilePath = @"C:\music\a.flac", Duration = TimeSpan.FromSeconds(252) };

        Assert.Equal("4:12", track.DisplayDuration);
    }

    [Fact]
    public void DisplaySubtitle_JoinsArtistAlbumFormat()
    {
        var track = new Track { FilePath = @"C:\music\a.flac", Artist = "陈粒", Album = "在蓬莱" };

        Assert.Equal("陈粒 · 在蓬莱 · FLAC", track.DisplaySubtitle);
    }
}
