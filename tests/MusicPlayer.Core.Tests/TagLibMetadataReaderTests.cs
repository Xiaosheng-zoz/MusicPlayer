using MusicPlayer.Core.Metadata;

namespace MusicPlayer.Core.Tests;

public class TagLibMetadataReaderTests
{
    [Fact]
    public void Read_FallsBackToFileName_WhenFileIsNotRealAudio()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mp-notaudio-{Guid.NewGuid():N}.mp3");
        File.WriteAllText(path, "definitely not an audio file");

        try
        {
            var track = new TagLibMetadataReader().Read(path);

            Assert.Equal(path, track.FilePath);
            Assert.Equal(Path.GetFileNameWithoutExtension(path), track.DisplayTitle);
            Assert.Equal("—", track.DisplayArtist);
            Assert.Equal("--:--", track.DisplayDuration);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_FallsBackToFileName_WhenFileDoesNotExist()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mp-missing-{Guid.NewGuid():N}.flac");

        var track = new TagLibMetadataReader().Read(path);

        Assert.Equal(Path.GetFileNameWithoutExtension(path), track.DisplayTitle);
    }

    [Fact]
    public void Read_ReadsDuration_FromRealSampleCopiedOutOfTheMusicLibrary()
    {
        const string library = @"E:\LocalMusic";
        if (!Directory.Exists(library))
        {
            return; // 本机没有这个目录时跳过（见 spec 第 2.3 节）
        }

        var source = Directory.EnumerateFiles(library, "*.flac", SearchOption.AllDirectories).FirstOrDefault();
        if (source is null)
        {
            return;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"mp-sample-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var copy = Path.Combine(tempDir, Path.GetFileName(source));
        File.Copy(source, copy); // 只读源目录：先复制，再读副本

        try
        {
            var track = new TagLibMetadataReader().Read(copy);

            Assert.True(track.Duration > TimeSpan.Zero, "真实 FLAC 样本应当能读出时长");
            Assert.False(string.IsNullOrWhiteSpace(track.DisplayTitle));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
