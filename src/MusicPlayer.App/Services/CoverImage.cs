using Microsoft.Maui.Controls;

namespace MusicPlayer.App.Services;

internal static class CoverImage
{
    /// <summary>把封面原始字节包成 ImageSource；没有封面时返回 null，由 XAML 里的底色兜底。</summary>
    public static ImageSource? FromBytes(byte[]? bytes)
        => bytes is { Length: > 0 } ? ImageSource.FromStream(() => new MemoryStream(bytes)) : null;
}
