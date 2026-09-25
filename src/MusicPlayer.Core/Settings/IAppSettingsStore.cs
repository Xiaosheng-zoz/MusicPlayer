namespace MusicPlayer.Core.Settings;

/// <summary>存放跨次启动要记住的少量设置。</summary>
public interface IAppSettingsStore
{
    /// <summary>设置持久化的位置，用于在界面上告诉用户文件在哪。</summary>
    string Location { get; }

    /// <summary>读取设置；文件不存在或读不出来时返回默认值。</summary>
    AppSettings Load();

    /// <summary>覆盖写入设置。</summary>
    void Save(AppSettings settings);
}
