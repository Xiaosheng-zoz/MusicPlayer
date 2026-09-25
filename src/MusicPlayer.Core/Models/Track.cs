namespace MusicPlayer.Core.Models;

/// <summary>一条音乐记录。所有显示相关字段都做了缺失降级，界面可以直接绑定。</summary>
public sealed record Track
{
    public required string FilePath { get; init; }

    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string Album { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }

    /// <summary>扩展名大写形式，例如 FLAC、MP3。</summary>
    public string Format => Path.GetExtension(FilePath).TrimStart('.').ToUpperInvariant();

    public string DisplayTitle =>
        string.IsNullOrWhiteSpace(Title) ? Path.GetFileNameWithoutExtension(FilePath) : Title;

    public string DisplayArtist => string.IsNullOrWhiteSpace(Artist) ? "—" : Artist;

    public string DisplayAlbum => string.IsNullOrWhiteSpace(Album) ? "—" : Album;

    public string DisplaySubtitle => $"{DisplayArtist} · {DisplayAlbum} · {Format}";

    public string DisplayDuration => Duration > TimeSpan.Zero
        ? $"{(int)Duration.TotalMinutes}:{Duration.Seconds:D2}"
        : "--:--";
}
