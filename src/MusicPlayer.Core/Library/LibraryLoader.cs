namespace MusicPlayer.Core.Library;

/// <summary>
/// 把"一个文件夹路径"变成"歌曲列表 + 该显示的提示语"。
/// 放在 Core 里是为了能单测——界面只负责把结果显示出来。
/// </summary>
public sealed class LibraryLoader
{
    private readonly ILibraryScanner _scanner;

    public LibraryLoader(ILibraryScanner scanner) => _scanner = scanner;

    public async Task<LibraryLoadResult> LoadAsync(string? folderPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return new LibraryLoadResult(
                Array.Empty<Models.Track>(),
                "请选择音乐文件夹",
                "点「浏览」选一个文件夹，或直接把路径粘贴到输入框里",
                FolderExists: false);
        }

        var path = folderPath.Trim();

        if (!Directory.Exists(path))
        {
            return new LibraryLoadResult(
                Array.Empty<Models.Track>(), "这个路径不存在", path, FolderExists: false);
        }

        var tracks = await _scanner.ScanAsync(path, cancellationToken);

        return tracks.Count == 0
            ? new LibraryLoadResult(tracks, "这个文件夹里没有 MP3 或 FLAC", path, FolderExists: true)
            : new LibraryLoadResult(tracks, "全部歌曲", $"{tracks.Count} 首 · {path}", FolderExists: true);
    }
}
