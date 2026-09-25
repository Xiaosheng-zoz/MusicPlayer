using MusicPlayer.Core.Library;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Tests;

public class TrackSearchTests
{
    private static Track Track(string title, string artist, string album)
        => new() { FilePath = $@"C:\m\{title}.flac", Title = title, Artist = artist, Album = album };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Matches_ReturnsTrue_WhenQueryIsEmpty(string? query)
    {
        Assert.True(TrackSearch.Matches(Track("夜航西飞", "陈粒", "在蓬莱"), query));
    }

    [Fact]
    public void Matches_ByTitleSubstring()
    {
        var track = Track("夜航西飞", "陈粒", "在蓬莱");

        Assert.True(TrackSearch.Matches(track, "西飞"));
    }

    [Fact]
    public void Matches_ByArtistSubstring()
    {
        var track = Track("夜航西飞", "陈粒", "在蓬莱");

        Assert.True(TrackSearch.Matches(track, "陈粒"));
    }

    [Fact]
    public void Matches_IsCaseInsensitive()
    {
        var track = Track("Nuvole Bianche", "Ludovico Einaudi", "Una Mattina");

        Assert.True(TrackSearch.Matches(track, "nuvole"));
        Assert.True(TrackSearch.Matches(track, "EINAUDI"));
    }

    [Fact]
    public void Matches_IgnoresSurroundingWhitespaceInQuery()
    {
        Assert.True(TrackSearch.Matches(Track("夜航西飞", "陈粒", "在蓬莱"), "  陈粒  "));
    }

    [Fact]
    public void Matches_DoesNotSearchAlbum()
    {
        // 需求只要求歌名和歌手，专辑不参与匹配
        var track = Track("夜航西飞", "陈粒", "在蓬莱");

        Assert.False(TrackSearch.Matches(track, "在蓬莱"));
    }

    [Fact]
    public void Matches_ReturnsFalse_WhenNothingContainsTheQuery()
    {
        Assert.False(TrackSearch.Matches(Track("夜航西飞", "陈粒", "在蓬莱"), "海阔天空"));
    }

    [Fact]
    public void Matches_FallsBackToFileName_WhenTitleIsMissing()
    {
        var track = new Track { FilePath = @"C:\m\Aimer - 六等星の夜.flac", Artist = "Aimer" };

        Assert.True(TrackSearch.Matches(track, "六等星"));
    }
}
