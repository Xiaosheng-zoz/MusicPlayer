namespace MusicPlayer.Core.Settings;

public sealed record AppSettings
{
    /// <summary>是否记住上次打开的文件夹。默认开。</summary>
    public bool RememberLastFolder { get; init; } = true;

    /// <summary>上次打开的音乐文件夹；没记过时为 null。</summary>
    public string? LastFolder { get; init; }
}
