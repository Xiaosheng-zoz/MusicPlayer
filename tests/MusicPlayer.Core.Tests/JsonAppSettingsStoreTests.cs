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
    public void Load_ReturnsDefaults_WhenFileDoesNotExist()
    {
        var settings = new JsonAppSettingsStore(_file).Load();

        Assert.True(settings.RememberLastFolder);
        Assert.Null(settings.LastFolder);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsBothValues()
    {
        var store = new JsonAppSettingsStore(_file);

        store.Save(new AppSettings { RememberLastFolder = false, LastFolder = @"E:\LocalMusic" });
        var loaded = new JsonAppSettingsStore(_file).Load();

        Assert.False(loaded.RememberLastFolder);
        Assert.Equal(@"E:\LocalMusic", loaded.LastFolder);
    }

    [Fact]
    public void Load_DefaultsRememberToTrue_WhenOlderFileHasNoSuchField()
    {
        // 这个功能是后加的：老版本写的文件里只有 LastFolder，没有开关字段。
        // 缺字段必须当作「记住」，否则用户升级后会发现自己的设置被悄悄关掉了。
        File.WriteAllText(_file, """{ "LastFolder": "E:\\LocalMusic" }""");

        var settings = new JsonAppSettingsStore(_file).Load();

        Assert.True(settings.RememberLastFolder);
        Assert.Equal(@"E:\LocalMusic", settings.LastFolder);
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenFileIsCorrupt()
    {
        File.WriteAllText(_file, "这不是 JSON {{{{");

        // 设置文件坏掉不该让应用起不来
        var settings = new JsonAppSettingsStore(_file).Load();

        Assert.True(settings.RememberLastFolder);
        Assert.Null(settings.LastFolder);
    }

    [Fact]
    public void Save_CreatesTheDirectoryIfMissing()
    {
        var nested = Path.Combine(_dir, "a", "b", "settings.json");
        var store = new JsonAppSettingsStore(nested);

        store.Save(new AppSettings { LastFolder = @"E:\LocalMusic" });

        Assert.True(File.Exists(nested));
        Assert.Equal(@"E:\LocalMusic", store.Load().LastFolder);
    }

    [Fact]
    public void Load_NormalisesBlankFolderToNull()
    {
        File.WriteAllText(_file, """{ "RememberLastFolder": true, "LastFolder": "   " }""");

        Assert.Null(new JsonAppSettingsStore(_file).Load().LastFolder);
    }
}
