using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Library;

public interface ILibraryScanner
{
    Task<IReadOnlyList<Track>> ScanAsync(string folderPath, CancellationToken cancellationToken = default);
}
