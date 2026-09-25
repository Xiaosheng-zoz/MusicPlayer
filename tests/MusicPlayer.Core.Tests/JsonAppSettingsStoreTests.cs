using MusicPlayer.Core.Settings;

namespace MusicPlayer.Core.Tests;

public class JsonAppSettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"mp-settings-{Guid.NewGuid():N}");
    private readonly string _file;

    public JsonAppSettingsStoreTests()
    {
        Directory.CreateDirectory(_dir);
        _file = Path.Combine(_dir, "settings.json");
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void LoadLastFolder_ReturnsNull_WhenFileDoesNotExist()
    {
        var store = new JsonAppSettingsStore(_file);

        Assert.Null(store.LoadLastFolder());
    }

    [Fact]
    public void SaveThenLoad_RoundTripsTheFolder()
    {
        var store = new JsonAppSettingsStore(_file);

        store.SaveLastFolder(@"E:\LocalMusic");

        Assert.Equal(@"E:\LocalMusic", new JsonAppSettingsStore(_file).LoadLastFolder());
    }

    [Fact]
    public void LoadLastFolder_ReturnsNull_WhenFileIsCorrupt()
    {
        File.WriteAllText(_file, "这不是 JSON {{{{");
        var store = new JsonAppSettingsStore(_file);

        // 设置文件坏掉不该让应用起不来
        Assert.Null(store.LoadLastFolder());
    }

    [Fact]
    public void SaveLastFolder_CreatesTheDirectoryIfMissing()
    {
        var nested = Path.Combine(_dir, "a", "b", "settings.json");
        var store = new JsonAppSettingsStore(nested);

        store.SaveLastFolder(@"E:\LocalMusic");

        Assert.True(File.Exists(nested));
        Assert.Equal(@"E:\LocalMusic", store.LoadLastFolder());
    }

    [Fact]
    public void LoadLastFolder_ReturnsNull_WhenSavedFolderIsBlank()
    {
        var store = new JsonAppSettingsStore(_file);

        store.SaveLastFolder("   ");

        Assert.Null(store.LoadLastFolder());
    }
}
