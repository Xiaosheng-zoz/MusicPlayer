using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Library;

/// <summary>
/// 列表搜索：只按歌名和歌手做不区分大小写的包含匹配。
/// 放在 Core 里是为了能单测——界面只负责把匹配结果显示出来。
/// </summary>
public static class TrackSearch
{
    public static bool Matches(Track track, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var needle = query.Trim();

        // DisplayTitle 在没有标签时会回退到文件名，所以搜文件名也能中
        return Contains(track.DisplayTitle, needle) || Contains(track.DisplayArtist, needle);
    }

    private static bool Contains(string source, string needle)
        => source.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
