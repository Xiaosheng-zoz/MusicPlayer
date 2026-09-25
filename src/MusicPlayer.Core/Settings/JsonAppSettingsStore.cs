using System.Text.Json;

namespace MusicPlayer.Core.Settings;

public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _filePath;

    public JsonAppSettingsStore(string filePath) => _filePath = filePath;

    public string Location => _filePath;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new AppSettings();
            }

            var file = JsonSerializer.Deserialize<SettingsFile>(File.ReadAllText(_filePath), Options);
            if (file is null)
            {
                return new AppSettings();
            }

            return new AppSettings
            {
                // 老版本写的文件里没有这个字段，缺省当作「记住」——
                // 否则用户升级后会发现自己的设置被悄悄关掉了
                RememberLastFolder = file.RememberLastFolder ?? true,
                LastFolder = string.IsNullOrWhiteSpace(file.LastFolder) ? null : file.LastFolder
            };
        }
        catch (Exception)
        {
            // 设置文件损坏或没有读权限都不该让应用起不来，当作默认设置即可
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var file = new SettingsFile
            {
                RememberLastFolder = settings.RememberLastFolder,
                LastFolder = settings.LastFolder
            };

            File.WriteAllText(_filePath, JsonSerializer.Serialize(file, Options));
        }
        catch (Exception)
        {
            // 写设置失败不该影响听歌
        }
    }

    private sealed class SettingsFile
    {
        public bool? RememberLastFolder { get; set; }

        public string? LastFolder { get; set; }
    }
}
