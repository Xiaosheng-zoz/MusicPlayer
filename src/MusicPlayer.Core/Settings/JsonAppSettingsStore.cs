using System.Text.Json;

namespace MusicPlayer.Core.Settings;

public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _filePath;

    public JsonAppSettingsStore(string filePath) => _filePath = filePath;

    public string? LoadLastFolder()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            var settings = JsonSerializer.Deserialize<SettingsFile>(File.ReadAllText(_filePath), Options);
            var folder = settings?.LastFolder;

            return string.IsNullOrWhiteSpace(folder) ? null : folder;
        }
        catch (Exception)
        {
            // 设置文件损坏或没有读权限都不该让应用起不来，当作"没设置过"即可
            return null;
        }
    }

    public void SaveLastFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                _filePath,
                JsonSerializer.Serialize(new SettingsFile { LastFolder = folderPath }, Options));
        }
        catch (Exception)
        {
            // 写设置失败不该影响听歌
        }
    }

    private sealed class SettingsFile
    {
        public string? LastFolder { get; set; }
    }
}
