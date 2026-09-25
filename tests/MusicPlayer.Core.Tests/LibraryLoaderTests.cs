using MusicPlayer.Core.Library;
using MusicPlayer.Core.Metadata;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Tests;

public class LibraryLoaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"mp-load-{Guid.NewGuid():N}");

    private sealed class StubMetadataReader : ITrackMetadataReader
    {
        public Track Read(string filePath) => new() { FilePath = filePath };
    }

    public LibraryLoaderTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private LibraryLoader CreateLoader() => new(new FolderLibraryScanner(new StubMetadataReader()));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LoadAsync_WithNoPath_AsksTheUserToPickOne(string? path)
    {
        var result = await CreateLoader().LoadAsync(path);

        Assert.Empty(result.Tracks);
        Assert.Equal("请选择音乐文件夹", result.StatusMessage);
    }

    [Fact]
    public async Task LoadAsync_WithMissingPath_SaysThePathDoesNotExist()
    {
        var missing = Path.Combine(_root, "no-such-folder");

        var result = await CreateLoader().LoadAsync(missing);

        Assert.Empty(result.Tracks);
        Assert.Equal("这个路径不存在", result.StatusMessage);
        Assert.Equal(missing, result.StatusDetail);
    }

    [Fact]
    public async Task LoadAsync_WithFolderThatHasNoAudio_SaysSo()
    {
        File.WriteAllText(Path.Combine(_root, "readme.txt"), "x");

        var result = await CreateLoader().LoadAsync(_root);

        Assert.Empty(result.Tracks);
        Assert.Equal("这个文件夹里没有 MP3 或 FLAC", result.StatusMessage);
    }

    [Fact]
    public async Task LoadAsync_WithAudio_ReportsTrackCount()
    {
        File.WriteAllText(Path.Combine(_root, "a.flac"), "x");
        File.WriteAllText(Path.Combine(_root, "b.mp3"), "x");

        var result = await CreateLoader().LoadAsync(_root);

        Assert.Equal(2, result.Tracks.Count);
        // 文件夹那一栏只需要报个数量，列表本身在「音乐」里
        Assert.Equal("2 首", result.StatusMessage);
        Assert.Equal(_root, result.StatusDetail);
    }

    [Fact]
    public async Task LoadAsync_WithExistingFolder_ReportsFolderExists()
    {
        File.WriteAllText(Path.Combine(_root, "a.flac"), "x");

        var result = await CreateLoader().LoadAsync(_root);

        Assert.True(result.FolderExists);
    }

    [Fact]
    public async Task LoadAsync_WithMissingPath_ReportsFolderMissing()
    {
        var result = await CreateLoader().LoadAsync(Path.Combine(_root, "no-such-folder"));

        Assert.False(result.FolderExists);
    }

    [Fact]
    public async Task LoadAsync_WithNoPath_ReportsFolderMissing()
    {
        var result = await CreateLoader().LoadAsync("  ");

        Assert.False(result.FolderExists);
    }
}
