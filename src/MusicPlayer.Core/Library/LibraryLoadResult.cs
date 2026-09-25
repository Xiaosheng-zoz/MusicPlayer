using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Library;

/// <summary>一次"打开文件夹"的结果：拿到什么歌，以及该对用户说什么。</summary>
public sealed record LibraryLoadResult(
    IReadOnlyList<Track> Tracks,
    string StatusMessage,
    string StatusDetail);
