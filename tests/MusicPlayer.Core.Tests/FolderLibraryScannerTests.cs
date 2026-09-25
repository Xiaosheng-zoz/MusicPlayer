using MusicPlayer.Core.Library;
using MusicPlayer.Core.Metadata;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Tests;

public class FolderLibraryScannerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"mp-scan-{Guid.NewGuid():N}");

    private sealed class StubMetadataReader : ITrackMetadataReader
    {
        public List<string> Seen { get; } = new();

        public Track Read(string filePath)
        {
            Seen.Add(filePath);
            return new Track { FilePath = filePath };
        }
    }

    public FolderLibraryScannerTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Touch(string relativePath)
    {
        var full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "x");
        return full;
    }

    [Fact]
    public async Task ScanAsync_CollectsOnlyMp3AndFlac()
    {
        Touch("a.flac");
        Touch("b.mp3");
        Touch("notes.txt");
        Touch("cover.jpg");

        var reader = new StubMetadataReader();
        var result = await new FolderLibraryScanner(reader).ScanAsync(_root);

        Assert.Equal(2, result.Count);
        Assert.Equal(2, reader.Seen.Count);
        Assert.Contains(result, t => t.Format == "FLAC");
        Assert.Contains(result, t => t.Format == "MP3");
    }

    [Fact]
    public async Task ScanAsync_RecursesIntoSubdirectories()
    {
        Touch("album1/one.flac");
        Touch("album1/disc2/two.flac");

        var result = await new FolderLibraryScanner(new StubMetadataReader()).ScanAsync(_root);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task ScanAsync_MatchesExtensionCaseInsensitively()
    {
        Touch("LOUD.FLAC");
        Touch("quiet.Mp3");

        var result = await new FolderLibraryScanner(new StubMetadataReader()).ScanAsync(_root);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task ScanAsync_ReturnsEmpty_WhenFolderHasNoAudio()
    {
        Touch("readme.txt");

        var result = await new FolderLibraryScanner(new StubMetadataReader()).ScanAsync(_root);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ScanAsync_ReturnsEmpty_WhenFolderDoesNotExist()
    {
        var missing = Path.Combine(_root, "no-such-folder");

        var result = await new FolderLibraryScanner(new StubMetadataReader()).ScanAsync(missing);

        Assert.Empty(result);
    }
}
