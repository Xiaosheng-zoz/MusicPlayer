using MusicPlayer.Core.Metadata;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Library;

public sealed class FolderLibraryScanner : ILibraryScanner
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".flac" };

    private readonly ITrackMetadataReader _metadataReader;

    public FolderLibraryScanner(ITrackMetadataReader metadataReader) => _metadataReader = metadataReader;

    public Task<IReadOnlyList<Track>> ScanAsync(string folderPath, CancellationToken cancellationToken = default)
        => Task.Run<IReadOnlyList<Track>>(() => Scan(folderPath, cancellationToken), cancellationToken);

    private IReadOnlyList<Track> Scan(string folderPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return Array.Empty<Track>();
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,                  // 无权限的子目录跳过，不中断整次扫描
            AttributesToSkip = FileAttributes.System
        };

        var tracks = new List<Track>();
        foreach (var path in Directory.EnumerateFiles(folderPath, "*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!SupportedExtensions.Contains(Path.GetExtension(path)))
            {
                continue;
            }

            tracks.Add(_metadataReader.Read(path));
        }

        return tracks;
    }
}
