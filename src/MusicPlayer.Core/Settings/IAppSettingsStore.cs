namespace MusicPlayer.Core.Settings;

/// <summary>存放跨次启动要记住的少量设置。</summary>
public interface IAppSettingsStore
{
    /// <summary>上次打开的音乐文件夹；没记过或读不出来时返回 null。</summary>
    string? LoadLastFolder();

    /// <summary>记住这次打开的文件夹。传空值表示不记。</summary>
    void SaveLastFolder(string folderPath);
}
