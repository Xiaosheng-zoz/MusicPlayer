# 本地音乐播放器 PC 端最小实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 Windows 上做出一个自己用的本地音乐播放器：选文件夹、列出 MP3/FLAC、显示元数据、点击播放，并支持上一首/下一首/进度/音量。

**Architecture:** 分三层。`MusicPlayer.Core` 是纯 .NET 类库，装所有不碰界面的逻辑（扫描、元数据、播放队列、播放编排），能被单元测试完整覆盖；`MusicPlayer.App` 是 MAUI 应用，只做界面和平台适配，播放能力通过 `IAudioPlayer` 接口注入；`MusicPlayer.Core.Tests` 用 xUnit 加一个假播放器验证 Core。这样将来做 iOS 端时可以整体复用 Core，只新增一个 `IAudioPlayer` 实现和界面适配。

**Tech Stack:** .NET 10 · C# · .NET MAUI（Windows）· TagLibSharp · CommunityToolkit.Mvvm · CommunityToolkit.Maui.MediaElement · xUnit

**Spec:** `docs/superpowers/specs/2026-09-25-local-music-player-pc-mvp-design.md`

## Global Constraints

- Core 与测试项目目标框架 `net10.0`；App 项目只保留 `net10.0-windows10.0.19041.0`，本阶段不要 Android / iOS TFM
- `MusicPlayer.Core` 不得引用任何 MAUI 或 UI 相关的包
- 只收集扩展名 `.mp3` 和 `.flac` 的文件，大小写不敏感
- **对音乐文件只读**：不写入、不删除、不移动、不修改标签
- 测试音乐必须从 `E:\LocalMusic\` 复制到临时目录后再使用，绝不在原目录上操作
- 界面配色：强调色 `#2F6FED`，侧栏底色 `#F5F6F8`，次级文字 `#8B93A2`
- 窗口最小尺寸 480 × 520
- 只装 `maui-windows` 工作负载，不要装完整的 `maui`
- NuGet 包一律安装最新稳定版（`dotnet add package <id>`，不带版本号）

## 文件结构

```
MusicPlayer.sln
├─ src/MusicPlayer.Core/                       # 纯逻辑，可单测
│   ├─ Models/Track.cs                         # 一条音乐记录 + 显示用的格式化属性
│   ├─ Library/ILibraryScanner.cs              # 扫描抽象
│   ├─ Library/FolderLibraryScanner.cs         # 递归扫描文件夹，只收 mp3/flac
│   ├─ Metadata/ITrackMetadataReader.cs        # 元数据读取抽象
│   ├─ Metadata/TagLibMetadataReader.cs        # TagLib# 实现 + 降级
│   └─ Playback/
│       ├─ PlaybackState.cs                    # Stopped / Playing / Paused
│       ├─ IAudioPlayer.cs                     # 唯一的平台播放能力抽象
│       ├─ PlaybackQueue.cs                    # 队列与上/下一首规则（纯逻辑）
│       └─ PlaybackController.cs               # 队列 + 播放器的编排门面
├─ src/MusicPlayer.App/                        # MAUI 应用，只做界面与平台适配
│   ├─ MauiProgram.cs                          # 依赖注入与初始化
│   ├─ ViewModels/PlayerViewModel.cs           # 界面状态与命令
│   ├─ Services/MediaElementAudioPlayer.cs     # IAudioPlayer 的 MAUI 实现
│   └─ Views/MainPage.xaml(.cs)                # 主界面
└─ tests/MusicPlayer.Core.Tests/
    ├─ Fakes/FakeAudioPlayer.cs                # 可编程的假播放器
    ├─ TrackTests.cs
    ├─ TagLibMetadataReaderTests.cs
    ├─ FolderLibraryScannerTests.cs
    ├─ PlaybackQueueTests.cs
    └─ PlaybackControllerTests.cs
```

---

### Task 1: 解决方案骨架与 Track 模型

**Files:**
- Create: `MusicPlayer.sln`
- Create: `src/MusicPlayer.Core/MusicPlayer.Core.csproj`
- Create: `src/MusicPlayer.Core/Models/Track.cs`
- Create: `tests/MusicPlayer.Core.Tests/MusicPlayer.Core.Tests.csproj`
- Create: `tests/MusicPlayer.Core.Tests/TrackTests.cs`

**Interfaces:**
- Consumes: 无
- Produces: `MusicPlayer.Core.Models.Track`，一个 `record`，属性 `FilePath` / `Title` / `Artist` / `Album` / `Duration` / `CoverArt`，只读计算属性 `Format` / `DisplayTitle` / `DisplayArtist` / `DisplayAlbum` / `DisplaySubtitle` / `DisplayDuration`。后续所有任务都用这些名字。

- [ ] **Step 1: 建解决方案与两个项目**

```powershell
cd E:\code\MusicPlayer
dotnet new sln -n MusicPlayer
dotnet new classlib -n MusicPlayer.Core -o src/MusicPlayer.Core -f net10.0
dotnet new xunit -n MusicPlayer.Core.Tests -o tests/MusicPlayer.Core.Tests -f net10.0
dotnet sln add src/MusicPlayer.Core/MusicPlayer.Core.csproj tests/MusicPlayer.Core.Tests/MusicPlayer.Core.Tests.csproj
dotnet add tests/MusicPlayer.Core.Tests/MusicPlayer.Core.Tests.csproj reference src/MusicPlayer.Core/MusicPlayer.Core.csproj
Remove-Item src/MusicPlayer.Core/Class1.cs
```

- [ ] **Step 2: 写失败的测试**

创建 `tests/MusicPlayer.Core.Tests/TrackTests.cs`：

```csharp
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Tests;

public class TrackTests
{
    [Fact]
    public void DisplayTitle_FallsBackToFileNameWithoutExtension_WhenTitleMissing()
    {
        var track = new Track { FilePath = @"C:\music\Aimer - 六等星の夜.flac" };

        Assert.Equal("Aimer - 六等星の夜", track.DisplayTitle);
    }

    [Fact]
    public void DisplayTitle_UsesTagTitle_WhenPresent()
    {
        var track = new Track { FilePath = @"C:\music\track01.flac", Title = "夜航西飞" };

        Assert.Equal("夜航西飞", track.DisplayTitle);
    }

    [Theory]
    [InlineData("", "—")]
    [InlineData("   ", "—")]
    [InlineData("陈粒", "陈粒")]
    public void DisplayArtist_ShowsPlaceholder_WhenMissing(string artist, string expected)
    {
        var track = new Track { FilePath = @"C:\music\a.flac", Artist = artist };

        Assert.Equal(expected, track.DisplayArtist);
    }

    [Fact]
    public void Format_IsUppercaseExtension()
    {
        Assert.Equal("FLAC", new Track { FilePath = @"C:\music\a.FLAC" }.Format);
        Assert.Equal("MP3", new Track { FilePath = @"C:\music\a.mp3" }.Format);
    }

    [Fact]
    public void DisplayDuration_ShowsPlaceholder_WhenDurationUnknown()
    {
        Assert.Equal("--:--", new Track { FilePath = @"C:\music\a.flac" }.DisplayDuration);
    }

    [Fact]
    public void DisplayDuration_FormatsAsMinutesAndSeconds()
    {
        var track = new Track { FilePath = @"C:\music\a.flac", Duration = TimeSpan.FromSeconds(252) };

        Assert.Equal("4:12", track.DisplayDuration);
    }

    [Fact]
    public void DisplaySubtitle_JoinsArtistAlbumFormat()
    {
        var track = new Track { FilePath = @"C:\music\a.flac", Artist = "陈粒", Album = "在蓬莱" };

        Assert.Equal("陈粒 · 在蓬莱 · FLAC", track.DisplaySubtitle);
    }
}
```

- [ ] **Step 3: 跑测试，确认它失败**

```powershell
dotnet test tests/MusicPlayer.Core.Tests
```

期望：编译失败，提示 `Track` 找不到。这正是我们要的信号。

- [ ] **Step 4: 实现 Track**

创建 `src/MusicPlayer.Core/Models/Track.cs`：

```csharp
namespace MusicPlayer.Core.Models;

/// <summary>一条音乐记录。所有显示相关字段都做了缺失降级，界面可以直接绑定。</summary>
public sealed record Track
{
    public required string FilePath { get; init; }

    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string Album { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public byte[]? CoverArt { get; init; }

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
```

- [ ] **Step 5: 跑测试，确认全绿**

```powershell
dotnet test tests/MusicPlayer.Core.Tests
```

期望：7 个测试全部通过。

- [ ] **Step 6: 提交**

```powershell
git add MusicPlayer.sln src tests
git commit -m "feat: 解决方案骨架与 Track 模型"
```

---

### Task 2: 元数据读取与降级

**Files:**
- Create: `src/MusicPlayer.Core/Metadata/ITrackMetadataReader.cs`
- Create: `src/MusicPlayer.Core/Metadata/TagLibMetadataReader.cs`
- Create: `tests/MusicPlayer.Core.Tests/TagLibMetadataReaderTests.cs`
- Modify: `src/MusicPlayer.Core/MusicPlayer.Core.csproj`（加 TagLibSharp 引用）

**Interfaces:**
- Consumes: `Track`
- Produces: `ITrackMetadataReader`，方法 `Track Read(string filePath)`。约定：**永不抛异常**，读不到就用文件名降级。

- [ ] **Step 1: 加依赖**

```powershell
dotnet add src/MusicPlayer.Core package TagLibSharp
```

- [ ] **Step 2: 写失败的测试**

创建 `tests/MusicPlayer.Core.Tests/TagLibMetadataReaderTests.cs`：

```csharp
using MusicPlayer.Core.Metadata;

namespace MusicPlayer.Core.Tests;

public class TagLibMetadataReaderTests
{
    [Fact]
    public void Read_FallsBackToFileName_WhenFileIsNotRealAudio()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mp-notaudio-{Guid.NewGuid():N}.mp3");
        File.WriteAllText(path, "definitely not an audio file");

        try
        {
            var track = new TagLibMetadataReader().Read(path);

            Assert.Equal(path, track.FilePath);
            Assert.Equal(Path.GetFileNameWithoutExtension(path), track.DisplayTitle);
            Assert.Equal("—", track.DisplayArtist);
            Assert.Equal("--:--", track.DisplayDuration);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_FallsBackToFileName_WhenFileDoesNotExist()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mp-missing-{Guid.NewGuid():N}.flac");

        var track = new TagLibMetadataReader().Read(path);

        Assert.Equal(Path.GetFileNameWithoutExtension(path), track.DisplayTitle);
    }

    [Fact]
    public void Read_ReadsDuration_FromRealSampleCopiedOutOfTheMusicLibrary()
    {
        const string library = @"E:\LocalMusic";
        if (!Directory.Exists(library))
        {
            return; // 本机没有这个目录时跳过（见 spec 第 2.3 节）
        }

        var source = Directory.EnumerateFiles(library, "*.flac", SearchOption.AllDirectories).FirstOrDefault();
        if (source is null)
        {
            return;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"mp-sample-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var copy = Path.Combine(tempDir, Path.GetFileName(source));
        File.Copy(source, copy); // 只读源目录：先复制，再读副本

        try
        {
            var track = new TagLibMetadataReader().Read(copy);

            Assert.True(track.Duration > TimeSpan.Zero, "真实 FLAC 样本应当能读出时长");
            Assert.False(string.IsNullOrWhiteSpace(track.DisplayTitle));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
```

- [ ] **Step 3: 跑测试，确认它失败**

```powershell
dotnet test tests/MusicPlayer.Core.Tests
```

期望：编译失败，提示 `TagLibMetadataReader` 找不到。

- [ ] **Step 4: 实现**

`src/MusicPlayer.Core/Metadata/ITrackMetadataReader.cs`：

```csharp
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Metadata;

/// <summary>读取单个文件的元数据。实现必须永不抛异常——单个坏文件不能中断整次扫描。</summary>
public interface ITrackMetadataReader
{
    Track Read(string filePath);
}
```

`src/MusicPlayer.Core/Metadata/TagLibMetadataReader.cs`：

```csharp
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Metadata;

public sealed class TagLibMetadataReader : ITrackMetadataReader
{
    public Track Read(string filePath)
    {
        try
        {
            // 必须写全名 TagLib.File，避免和 System.IO.File 混淆
            using var file = TagLib.File.Create(filePath);

            return new Track
            {
                FilePath = filePath,
                Title = file.Tag?.Title ?? string.Empty,
                Artist = file.Tag?.FirstPerformer ?? string.Empty,
                Album = file.Tag?.Album ?? string.Empty,
                Duration = file.Properties?.Duration ?? TimeSpan.Zero,
                CoverArt = file.Tag?.Pictures?.FirstOrDefault()?.Data?.Data
            };
        }
        catch (Exception)
        {
            // 损坏文件、格式不符、文件不存在都会走到这里。回退到文件名即可（见 spec 第 6 节）。
            return new Track { FilePath = filePath };
        }
    }
}
```

- [ ] **Step 5: 跑测试，确认全绿**

```powershell
dotnet test tests/MusicPlayer.Core.Tests
```

期望：10 个测试全部通过。第三个测试会真的从 `E:\LocalMusic\` 复制一个 FLAC 出来读——这是唯一能证明 TagLib 读得动你那些高码率 FLAC 的办法。

- [ ] **Step 6: 提交**

```powershell
git add src tests
git commit -m "feat: 用 TagLib# 读取元数据，读不到时降级到文件名"
```

---

### Task 3: 文件夹扫描器

**Files:**
- Create: `src/MusicPlayer.Core/Library/ILibraryScanner.cs`
- Create: `src/MusicPlayer.Core/Library/FolderLibraryScanner.cs`
- Create: `tests/MusicPlayer.Core.Tests/FolderLibraryScannerTests.cs`

**Interfaces:**
- Consumes: `Track`、`ITrackMetadataReader`
- Produces: `ILibraryScanner`，方法 `Task<IReadOnlyList<Track>> ScanAsync(string folderPath, CancellationToken cancellationToken = default)`

- [ ] **Step 1: 写失败的测试**

创建 `tests/MusicPlayer.Core.Tests/FolderLibraryScannerTests.cs`：

```csharp
using MusicPlayer.Core.Library;
using MusicPlayer.Core.Metadata;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Tests;

public class FolderLibraryScannerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"mp-scan-{Guid.NewGuid():N}");

    private sealed class StubMetadataReader : ITrackMetadataReader
    {
        public List<string> Seen { get; } = new();

        public Track Read(string filePath)
        {
            Seen.Add(filePath);
            return new Track { FilePath = filePath };
        }
    }

    public FolderLibraryScannerTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Touch(string relativePath)
    {
        var full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "x");
        return full;
    }

    [Fact]
    public async Task ScanAsync_CollectsOnlyMp3AndFlac()
    {
        Touch("a.flac");
        Touch("b.mp3");
        Touch("notes.txt");
        Touch("cover.jpg");

        var reader = new StubMetadataReader();
        var result = await new FolderLibraryScanner(reader).ScanAsync(_root);

        Assert.Equal(2, result.Count);
        Assert.Equal(2, reader.Seen.Count);
        Assert.Contains(result, t => t.Format == "FLAC");
        Assert.Contains(result, t => t.Format == "MP3");
    }

    [Fact]
    public async Task ScanAsync_RecursesIntoSubdirectories()
    {
        Touch("album1/one.flac");
        Touch("album1/disc2/two.flac");

        var result = await new FolderLibraryScanner(new StubMetadataReader()).ScanAsync(_root);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task ScanAsync_MatchesExtensionCaseInsensitively()
    {
        Touch("LOUD.FLAC");
        Touch("quiet.Mp3");

        var result = await new FolderLibraryScanner(new StubMetadataReader()).ScanAsync(_root);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task ScanAsync_ReturnsEmpty_WhenFolderHasNoAudio()
    {
        Touch("readme.txt");

        var result = await new FolderLibraryScanner(new StubMetadataReader()).ScanAsync(_root);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ScanAsync_ReturnsEmpty_WhenFolderDoesNotExist()
    {
        var missing = Path.Combine(_root, "no-such-folder");

        var result = await new FolderLibraryScanner(new StubMetadataReader()).ScanAsync(missing);

        Assert.Empty(result);
    }
}
```

- [ ] **Step 2: 跑测试，确认它失败**

```powershell
dotnet test tests/MusicPlayer.Core.Tests
```

期望：编译失败，提示 `FolderLibraryScanner` 找不到。

- [ ] **Step 3: 实现**

`src/MusicPlayer.Core/Library/ILibraryScanner.cs`：

```csharp
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Library;

public interface ILibraryScanner
{
    Task<IReadOnlyList<Track>> ScanAsync(string folderPath, CancellationToken cancellationToken = default);
}
```

`src/MusicPlayer.Core/Library/FolderLibraryScanner.cs`：

```csharp
using MusicPlayer.Core.Metadata;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Library;

public sealed class FolderLibraryScanner : ILibraryScanner
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".flac" };

    private readonly ITrackMetadataReader _metadataReader;

    public FolderLibraryScanner(ITrackMetadataReader metadataReader) => _metadataReader = metadataReader;

    public Task<IReadOnlyList<Track>> ScanAsync(string folderPath, CancellationToken cancellationToken = default)
        => Task.Run<IReadOnlyList<Track>>(() => Scan(folderPath, cancellationToken), cancellationToken);

    private IReadOnlyList<Track> Scan(string folderPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return Array.Empty<Track>();
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,                  // 无权限的子目录跳过，不中断整次扫描
            AttributesToSkip = FileAttributes.System
        };

        var tracks = new List<Track>();
        foreach (var path in Directory.EnumerateFiles(folderPath, "*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!SupportedExtensions.Contains(Path.GetExtension(path)))
            {
                continue;
            }

            tracks.Add(_metadataReader.Read(path));
        }

        return tracks;
    }
}
```

- [ ] **Step 4: 跑测试，确认全绿**

```powershell
dotnet test tests/MusicPlayer.Core.Tests
```

期望：15 个测试全部通过。注意最后一个测试证明"目录不存在"返回空列表而不是抛异常——界面靠这个显示空状态。

- [ ] **Step 5: 提交**

```powershell
git add src tests
git commit -m "feat: 递归扫描文件夹，只收集 mp3 与 flac"
```

---

### Task 4: 播放队列

**Files:**
- Create: `src/MusicPlayer.Core/Playback/PlaybackQueue.cs`
- Create: `tests/MusicPlayer.Core.Tests/PlaybackQueueTests.cs`

**Interfaces:**
- Consumes: `Track`
- Produces: `PlaybackQueue`，成员 `IReadOnlyList<Track> Tracks`、`int CurrentIndex`、`Track? Current`、`bool IsEmpty`、`void Replace(IEnumerable<Track>)`、`Track? SetCurrent(int index)`、`bool MoveNext()`、`bool MovePrevious()`、`void Clear()`

- [ ] **Step 1: 写失败的测试**

创建 `tests/MusicPlayer.Core.Tests/PlaybackQueueTests.cs`：

```csharp
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Playback;

namespace MusicPlayer.Core.Tests;

public class PlaybackQueueTests
{
    private static List<Track> ThreeTracks() => new()
    {
        new Track { FilePath = @"C:\m\1.flac" },
        new Track { FilePath = @"C:\m\2.flac" },
        new Track { FilePath = @"C:\m\3.flac" }
    };

    [Fact]
    public void NewQueue_IsEmpty_AndHasNoCurrent()
    {
        var queue = new PlaybackQueue();

        Assert.True(queue.IsEmpty);
        Assert.Null(queue.Current);
        Assert.Equal(-1, queue.CurrentIndex);
    }

    [Fact]
    public void SetCurrent_ReturnsTrack_AndUpdatesIndex()
    {
        var queue = new PlaybackQueue();
        queue.Replace(ThreeTracks());

        var current = queue.SetCurrent(1);

        Assert.Equal(@"C:\m\2.flac", current!.FilePath);
        Assert.Equal(1, queue.CurrentIndex);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void SetCurrent_ReturnsNull_ForOutOfRangeIndex(int index)
    {
        var queue = new PlaybackQueue();
        queue.Replace(ThreeTracks());

        Assert.Null(queue.SetCurrent(index));
        Assert.Equal(-1, queue.CurrentIndex);
    }

    [Fact]
    public void MoveNext_AdvancesAndReturnsTrue()
    {
        var queue = new PlaybackQueue();
        queue.Replace(ThreeTracks());
        queue.SetCurrent(0);

        Assert.True(queue.MoveNext());
        Assert.Equal(1, queue.CurrentIndex);
    }

    [Fact]
    public void MoveNext_ReturnsFalse_AtEndOfQueue_AndStaysPut()
    {
        var queue = new PlaybackQueue();
        queue.Replace(ThreeTracks());
        queue.SetCurrent(2);

        Assert.False(queue.MoveNext());
        Assert.Equal(2, queue.CurrentIndex);
    }

    [Fact]
    public void MovePrevious_ReturnsFalse_AtStartOfQueue_AndStaysPut()
    {
        var queue = new PlaybackQueue();
        queue.Replace(ThreeTracks());
        queue.SetCurrent(0);

        Assert.False(queue.MovePrevious());
        Assert.Equal(0, queue.CurrentIndex);
    }

    [Fact]
    public void SingleTrackQueue_CannotMoveEitherWay()
    {
        var queue = new PlaybackQueue();
        queue.Replace(new List<Track> { new() { FilePath = @"C:\m\only.flac" } });
        queue.SetCurrent(0);

        Assert.False(queue.MoveNext());
        Assert.False(queue.MovePrevious());
    }

    [Fact]
    public void Replace_ResetsCurrentIndex()
    {
        var queue = new PlaybackQueue();
        queue.Replace(ThreeTracks());
        queue.SetCurrent(2);

        queue.Replace(ThreeTracks());

        Assert.Equal(-1, queue.CurrentIndex);
        Assert.Null(queue.Current);
    }
}
```

- [ ] **Step 2: 跑测试，确认它失败**

```powershell
dotnet test tests/MusicPlayer.Core.Tests
```

期望：编译失败，提示 `PlaybackQueue` 找不到。

- [ ] **Step 3: 实现**

创建 `src/MusicPlayer.Core/Playback/PlaybackQueue.cs`：

```csharp
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Playback;

/// <summary>播放队列与上/下一首规则。纯逻辑，不碰解码器，可以被完整单测。</summary>
public sealed class PlaybackQueue
{
    private readonly List<Track> _tracks = new();

    public IReadOnlyList<Track> Tracks => _tracks;

    public int CurrentIndex { get; private set; } = -1;

    public bool IsEmpty => _tracks.Count == 0;

    public Track? Current =>
        CurrentIndex >= 0 && CurrentIndex < _tracks.Count ? _tracks[CurrentIndex] : null;

    public void Replace(IEnumerable<Track> tracks)
    {
        _tracks.Clear();
        _tracks.AddRange(tracks);
        CurrentIndex = -1;
    }

    public Track? SetCurrent(int index)
    {
        if (index < 0 || index >= _tracks.Count)
        {
            return null;
        }

        CurrentIndex = index;
        return Current;
    }

    /// <summary>前进一首。已经在末尾时返回 false 且不动。</summary>
    public bool MoveNext()
    {
        if (CurrentIndex + 1 >= _tracks.Count)
        {
            return false;
        }

        CurrentIndex++;
        return true;
    }

    /// <summary>后退一首。已经在开头时返回 false 且不动。</summary>
    public bool MovePrevious()
    {
        if (CurrentIndex - 1 < 0)
        {
            return false;
        }

        CurrentIndex--;
        return true;
    }

    public void Clear()
    {
        _tracks.Clear();
        CurrentIndex = -1;
    }
}
```

- [ ] **Step 4: 跑测试，确认全绿**

```powershell
dotnet test tests/MusicPlayer.Core.Tests
```

期望：25 个测试全部通过。

- [ ] **Step 5: 提交**

```powershell
git add src tests
git commit -m "feat: 播放队列与上/下一首规则"
```

---

### Task 5: 播放器抽象与播放编排

**Files:**
- Create: `src/MusicPlayer.Core/Playback/PlaybackState.cs`
- Create: `src/MusicPlayer.Core/Playback/IAudioPlayer.cs`
- Create: `src/MusicPlayer.Core/Playback/PlaybackController.cs`
- Create: `tests/MusicPlayer.Core.Tests/Fakes/FakeAudioPlayer.cs`
- Create: `tests/MusicPlayer.Core.Tests/PlaybackControllerTests.cs`

**Interfaces:**
- Consumes: `Track`、`PlaybackQueue`
- Produces:
  - `enum PlaybackState { Stopped, Playing, Paused }`
  - `IAudioPlayer`：`TimeSpan Position`、`TimeSpan Duration`、`double Volume`、`PlaybackState State`、事件 `Ended` / `PositionChanged` / `StateChanged` / `Failed(string)`、方法 `Load(string filePath)` / `Play()` / `Pause()` / `Stop()` / `Seek(TimeSpan)`
  - `PlaybackController`：`IReadOnlyList<Track> Tracks`、`Track? Current`、`PlaybackState State`、`TimeSpan Position`、`TimeSpan Duration`、`double Volume`、事件 `StateChanged` / `PositionChanged` / `PlaybackFailed(string)`、方法 `LoadTracks(IEnumerable<Track>)` / `bool PlayAt(int)` / `bool Next()` / `bool Previous()` / `void TogglePlayPause()` / `void Seek(TimeSpan)`

- [ ] **Step 1: 写假播放器**

创建 `tests/MusicPlayer.Core.Tests/Fakes/FakeAudioPlayer.cs`：

```csharp
using MusicPlayer.Core.Playback;

namespace MusicPlayer.Core.Tests.Fakes;

/// <summary>可编程的假播放器：记录调用序列，并可以指定哪些路径播放失败。</summary>
internal sealed class FakeAudioPlayer : IAudioPlayer
{
    public List<string> LoadedPaths { get; } = new();
    public HashSet<string> FailingPaths { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int PlayCount { get; private set; }

    public TimeSpan Position { get; private set; }
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(4);
    public double Volume { get; set; } = 0.8;
    public PlaybackState State { get; private set; } = PlaybackState.Stopped;

    public event EventHandler? Ended;
    public event EventHandler? PositionChanged;
    public event EventHandler? StateChanged;
    public event EventHandler<string>? Failed;

    public void Load(string filePath)
    {
        LoadedPaths.Add(filePath);

        if (FailingPaths.Contains(filePath))
        {
            Failed?.Invoke(this, $"无法播放 {Path.GetFileName(filePath)}");
        }
    }

    public void Play()
    {
        PlayCount++;
        State = PlaybackState.Playing;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pause()
    {
        State = PlaybackState.Paused;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        State = PlaybackState.Stopped;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Seek(TimeSpan position)
    {
        Position = position;
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>测试用：模拟一首歌播完。</summary>
    public void SimulateEnded() => Ended?.Invoke(this, EventArgs.Empty);
}
```

- [ ] **Step 2: 写失败的测试**

创建 `tests/MusicPlayer.Core.Tests/PlaybackControllerTests.cs`：

```csharp
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Playback;
using MusicPlayer.Core.Tests.Fakes;

namespace MusicPlayer.Core.Tests;

public class PlaybackControllerTests
{
    private static List<Track> ThreeTracks() => new()
    {
        new Track { FilePath = @"C:\m\1.flac" },
        new Track { FilePath = @"C:\m\2.flac" },
        new Track { FilePath = @"C:\m\3.flac" }
    };

    [Fact]
    public void PlayAt_LoadsThenPlays()
    {
        var player = new FakeAudioPlayer();
        var controller = new PlaybackController(player);
        controller.LoadTracks(ThreeTracks());

        Assert.True(controller.PlayAt(0));

        Assert.Equal(new[] { @"C:\m\1.flac" }, player.LoadedPaths);
        Assert.Equal(1, player.PlayCount);
        Assert.Equal(PlaybackState.Playing, controller.State);
        Assert.Equal("1", controller.Current!.DisplayTitle);
    }

    [Fact]
    public void PlayAt_ReturnsFalse_ForOutOfRangeIndex()
    {
        var controller = new PlaybackController(new FakeAudioPlayer());
        controller.LoadTracks(ThreeTracks());

        Assert.False(controller.PlayAt(9));
    }

    [Fact]
    public void Ended_AdvancesToNextTrack()
    {
        var player = new FakeAudioPlayer();
        var controller = new PlaybackController(player);
        controller.LoadTracks(ThreeTracks());
        controller.PlayAt(0);

        player.SimulateEnded();

        Assert.Equal(@"C:\m\2.flac", controller.Current!.FilePath);
        Assert.Equal(2, player.PlayCount);
    }

    [Fact]
    public void Ended_OnLastTrack_StopsPlayback()
    {
        var player = new FakeAudioPlayer();
        var controller = new PlaybackController(player);
        controller.LoadTracks(ThreeTracks());
        controller.PlayAt(2);

        player.SimulateEnded();

        Assert.Equal(PlaybackState.Stopped, controller.State);
        Assert.Equal(@"C:\m\3.flac", controller.Current!.FilePath);
    }

    [Fact]
    public void UnplayableFile_SkipsToNextTrack()
    {
        var player = new FakeAudioPlayer();
        player.FailingPaths.Add(@"C:\m\1.flac");
        var controller = new PlaybackController(player);
        controller.LoadTracks(ThreeTracks());

        controller.PlayAt(0);

        Assert.Equal(@"C:\m\2.flac", controller.Current!.FilePath);
        Assert.Equal(1, player.PlayCount);
    }

    [Fact]
    public void AllFilesUnplayable_ReportsFailureOnceAndStops()
    {
        var player = new FakeAudioPlayer();
        foreach (var track in ThreeTracks())
        {
            player.FailingPaths.Add(track.FilePath);
        }

        var controller = new PlaybackController(player);
        var failures = new List<string>();
        controller.PlaybackFailed += (_, message) => failures.Add(message);
        controller.LoadTracks(ThreeTracks());

        controller.PlayAt(0);

        Assert.Single(failures);
        Assert.Equal(PlaybackState.Stopped, controller.State);
        Assert.Equal(3, player.LoadedPaths.Count); // 三首都试过了才放弃
    }

    [Fact]
    public void TogglePlayPause_PausesWhenPlaying_AndResumesWhenPaused()
    {
        var player = new FakeAudioPlayer();
        var controller = new PlaybackController(player);
        controller.LoadTracks(ThreeTracks());
        controller.PlayAt(0);

        controller.TogglePlayPause();
        Assert.Equal(PlaybackState.Paused, controller.State);

        controller.TogglePlayPause();
        Assert.Equal(PlaybackState.Playing, controller.State);
    }

    [Fact]
    public void Volume_IsClampedToZeroAndOne()
    {
        var player = new FakeAudioPlayer();
        var controller = new PlaybackController(player);

        controller.Volume = 1.7;
        Assert.Equal(1.0, player.Volume);

        controller.Volume = -0.4;
        Assert.Equal(0.0, player.Volume);
    }

    [Fact]
    public void Seek_IsClampedToTrackBounds()
    {
        var player = new FakeAudioPlayer { Duration = TimeSpan.FromMinutes(3) };
        var controller = new PlaybackController(player);
        controller.LoadTracks(ThreeTracks());
        controller.PlayAt(0);

        controller.Seek(TimeSpan.FromMinutes(99));
        Assert.Equal(TimeSpan.FromMinutes(3), player.Position);

        controller.Seek(TimeSpan.FromSeconds(-5));
        Assert.Equal(TimeSpan.Zero, player.Position);
    }
}
```

- [ ] **Step 3: 跑测试，确认它失败**

```powershell
dotnet test tests/MusicPlayer.Core.Tests
```

期望：编译失败，提示 `PlaybackController` 与 `IAudioPlayer` 找不到。

- [ ] **Step 4: 实现**

`src/MusicPlayer.Core/Playback/PlaybackState.cs`：

```csharp
namespace MusicPlayer.Core.Playback;

public enum PlaybackState
{
    Stopped,
    Playing,
    Paused
}
```

`src/MusicPlayer.Core/Playback/IAudioPlayer.cs`：

```csharp
namespace MusicPlayer.Core.Playback;

/// <summary>
/// 唯一接触平台播放器的地方。PC 端用 MAUI 的 MediaElement 实现，
/// 将来换 LibVLC 或做 iOS 端都只是多一个实现类。
/// </summary>
public interface IAudioPlayer
{
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    double Volume { get; set; }
    PlaybackState State { get; }

    event EventHandler? Ended;
    event EventHandler? PositionChanged;
    event EventHandler? StateChanged;

    /// <summary>播放失败时触发，参数是给用户看的错误信息。</summary>
    event EventHandler<string>? Failed;

    void Load(string filePath);
    void Play();
    void Pause();
    void Stop();
    void Seek(TimeSpan position);
}
```

`src/MusicPlayer.Core/Playback/PlaybackController.cs`：

```csharp
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Playback;

/// <summary>把队列和播放器编在一起，对界面暴露一个门面。</summary>
public sealed class PlaybackController
{
    private readonly PlaybackQueue _queue = new();
    private readonly IAudioPlayer _player;
    private int _failedAttempts;

    public PlaybackController(IAudioPlayer player)
    {
        _player = player;
        _player.Ended += OnEnded;
        _player.Failed += OnFailed;
        _player.PositionChanged += (_, _) => PositionChanged?.Invoke(this, _player.Position);
        _player.StateChanged += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? StateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;

    /// <summary>队列里的歌全部播不了时触发一次，参数是给用户看的错误信息。</summary>
    public event EventHandler<string>? PlaybackFailed;

    public IReadOnlyList<Track> Tracks => _queue.Tracks;
    public Track? Current => _queue.Current;
    public PlaybackState State => _player.State;
    public TimeSpan Position => _player.Position;
    public TimeSpan Duration => _player.Duration;

    public double Volume
    {
        get => _player.Volume;
        set => _player.Volume = Math.Clamp(value, 0d, 1d);
    }

    public void LoadTracks(IEnumerable<Track> tracks)
    {
        _queue.Replace(tracks);
        _failedAttempts = 0;
    }

    public bool PlayAt(int index)
    {
        if (_queue.SetCurrent(index) is null)
        {
            return false;
        }

        StartCurrent();
        return true;
    }

    public bool Next() => _queue.MoveNext() && StartCurrent();

    public bool Previous() => _queue.MovePrevious() && StartCurrent();

    public void TogglePlayPause()
    {
        if (_player.State == PlaybackState.Playing)
        {
            _player.Pause();
        }
        else if (_queue.Current is not null)
        {
            _player.Play();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Seek(TimeSpan position)
    {
        if (_queue.Current is null)
        {
            return;
        }

        var clamped = position < TimeSpan.Zero
            ? TimeSpan.Zero
            : Duration > TimeSpan.Zero && position > Duration ? Duration : position;

        _player.Seek(clamped);
    }

    private bool StartCurrent()
    {
        var track = _queue.Current;
        if (track is null)
        {
            return false;
        }

        _player.Load(track.FilePath);
        _player.Play();
        StateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void OnEnded(object? sender, EventArgs e)
    {
        _failedAttempts = 0;

        if (!Next())
        {
            _player.Stop();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnFailed(object? sender, string message)
    {
        _failedAttempts++;
        StateChanged?.Invoke(this, EventArgs.Empty);

        // 整个队列都试过一遍还是不行，就停下来告诉用户，不要无限重试
        if (_failedAttempts >= _queue.Tracks.Count)
        {
            _player.Stop();
            PlaybackFailed?.Invoke(this, $"这个列表里的歌都播不了：{message}");
            return;
        }

        if (Next())
        {
            return;
        }

        _player.Stop();
        PlaybackFailed?.Invoke(this, message);
    }
}
```

- [ ] **Step 5: 跑测试，确认全绿**

```powershell
dotnet test tests/MusicPlayer.Core.Tests
```

期望：34 个测试全部通过。Core 到这里就完成了，后面全是界面和平台适配。

- [ ] **Step 6: 提交**

```powershell
git add src tests
git commit -m "feat: 播放器抽象与播放编排，坏文件自动跳过"
```

---

### Task 6: MAUI 应用骨架、选文件夹与歌曲列表

**Files:**
- Create: `src/MusicPlayer.App/`（由模板生成后裁剪）
- Modify: `src/MusicPlayer.App/MusicPlayer.App.csproj`
- Create: `src/MusicPlayer.App/ViewModels/PlayerViewModel.cs`
- Create: `src/MusicPlayer.App/Views/MainPage.xaml` 与 `MainPage.xaml.cs`
- Modify: `src/MusicPlayer.App/MauiProgram.cs` 与 `App.xaml.cs`

**Interfaces:**
- Consumes: `ILibraryScanner`、`FolderLibraryScanner`、`TagLibMetadataReader`
- Produces:
  - `TrackItem`：`Track Track`、`string Title`、`string Subtitle`、`string Duration`、`bool IsCurrent`
  - `PlayerViewModel`：`ObservableCollection<TrackItem> Tracks`、`bool HasTracks`、`bool IsBusy`、`bool IsEmpty`、`string StatusMessage`、`string StatusDetail`、命令 `ChooseFolderCommand`、`PlayTrackCommand`

**关于标题栏的决定：** 这一阶段用**系统原生标题栏**，不做自定义标题栏。原生标题栏右上角自带最小化、最大化、关闭，行为（贴边、快照布局、DPI、多显示器）全部由 Windows 负责。自制标题栏要写不少平台代码，而且很容易在这些细节上出错，对自用工具不划算。

- [ ] **Step 1: 安装工作负载并生成项目**

```powershell
dotnet workload install maui-windows
cd E:\code\MusicPlayer
dotnet new maui -n MusicPlayer.App -o src/MusicPlayer.App
dotnet sln add src/MusicPlayer.App/MusicPlayer.App.csproj
```

如果 `dotnet workload install` 报权限错误，用管理员权限的终端重跑这一条命令。

- [ ] **Step 2: 裁剪成只有 Windows 目标**

打开 `src/MusicPlayer.App/MusicPlayer.App.csproj`，把目标框架那一行（模板默认是 android/ios/maccatalyst/windows 四合一）替换为：

```xml
<TargetFrameworks>net10.0-windows10.0.19041.0</TargetFrameworks>
```

在同一个 `PropertyGroup` 里加上这两行——`WindowsPackageType=None` 让 `dotnet run` 直接跑免打包版本，`WindowsAppSDKSelfContained` 免去单独安装 Windows App SDK 运行时：

```xml
<WindowsPackageType>None</WindowsPackageType>
<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
```

删掉用不到的平台目录：

```powershell
Remove-Item -Recurse -Force src/MusicPlayer.App/Platforms/Android, src/MusicPlayer.App/Platforms/iOS, src/MusicPlayer.App/Platforms/MacCatalyst, src/MusicPlayer.App/Platforms/Tizen
```

- [ ] **Step 3: 加包引用**

```powershell
dotnet add src/MusicPlayer.App package CommunityToolkit.Mvvm
dotnet add src/MusicPlayer.App package CommunityToolkit.Maui
dotnet add src/MusicPlayer.App package CommunityToolkit.Maui.MediaElement
dotnet add src/MusicPlayer.App reference src/MusicPlayer.Core/MusicPlayer.Core.csproj
```

- [ ] **Step 4: 确认模板项目能编译**

```powershell
dotnet build src/MusicPlayer.App
```

期望：Build succeeded。先确认工具链通了，再动代码。

- [ ] **Step 5: 写 PlayerViewModel**

创建 `src/MusicPlayer.App/ViewModels/PlayerViewModel.cs`：

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicPlayer.Core.Library;
using MusicPlayer.Core.Models;

namespace MusicPlayer.App.ViewModels;

public sealed partial class TrackItem : ObservableObject
{
    public TrackItem(Track track) => Track = track;

    public Track Track { get; }
    public string Title => Track.DisplayTitle;
    public string Subtitle => Track.DisplaySubtitle;
    public string Duration => Track.DisplayDuration;

    [ObservableProperty]
    private bool _isCurrent;
}

public sealed partial class PlayerViewModel : ObservableObject
{
    private readonly ILibraryScanner _scanner;

    public PlayerViewModel(ILibraryScanner scanner) => _scanner = scanner;

    public ObservableCollection<TrackItem> Tracks { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _hasTracks;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isBusy;

    [ObservableProperty] private string _statusMessage = "还没有音乐";
    [ObservableProperty] private string _statusDetail = "选一个存放音乐的文件夹，支持 MP3 和 FLAC";

    public bool IsEmpty => !HasTracks && !IsBusy;

    [RelayCommand]
    private async Task ChooseFolderAsync()
    {
        var picked = await FolderPicker.Default.PickAsync(CancellationToken.None);
        if (!picked.IsSuccessful || picked.Folder is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "正在扫描…";
        StatusDetail = picked.Folder.Path;

        try
        {
            var tracks = await _scanner.ScanAsync(picked.Folder.Path);

            Tracks.Clear();
            foreach (var track in tracks)
            {
                Tracks.Add(new TrackItem(track));
            }

            HasTracks = Tracks.Count > 0;

            if (HasTracks)
            {
                StatusMessage = "全部歌曲";
                StatusDetail = $"{Tracks.Count} 首";
            }
            else
            {
                StatusMessage = "这个文件夹里没有 MP3 或 FLAC";
                StatusDetail = picked.Folder.Path;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void PlayTrack(TrackItem? item)
    {
        if (item is null)
        {
            return;
        }

        foreach (var track in Tracks)
        {
            track.IsCurrent = ReferenceEquals(track, item);
        }
    }
}
```

- [ ] **Step 6: 写主界面**

删掉模板生成的 `MainPage.xaml` / `MainPage.xaml.cs`（以及 `AppShell.xaml` / `AppShell.xaml.cs`），新建 `src/MusicPlayer.App/Views/MainPage.xaml`：

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:MusicPlayer.App.ViewModels"
             x:Class="MusicPlayer.App.Views.MainPage"
             x:Name="PageRoot"
             Title="音乐播放器"
             BackgroundColor="#FFFFFF">

  <Grid ColumnDefinitions="132,*" x:Name="RootGrid">

    <!-- 侧栏：本阶段只有"资料库"是活的，其余按最终形态摆好但禁用 -->
    <VerticalStackLayout Grid.Column="0" x:Name="Sidebar"
                         BackgroundColor="#F5F6F8" Padding="8,10" Spacing="2">
      <Label x:Name="SidePlaying"  Text="▶   正在播放" Padding="9,7" FontSize="11.5" TextColor="#5B6270" Opacity="0.45" />
      <Label x:Name="SideLibrary"  Text="♪   资料库"   Padding="9,7" FontSize="11.5" TextColor="#2F6FED" BackgroundColor="#E7EEFC" />
      <Label x:Name="SideFolder"   Text="▤   文件夹"   Padding="9,7" FontSize="11.5" TextColor="#5B6270" Opacity="0.45" />
      <Label x:Name="SidePlaylist" Text="≡   歌单"     Padding="9,7" FontSize="11.5" TextColor="#5B6270" Opacity="0.45" />
      <Label x:Name="SideSettings" Text="⚙   设置"     Padding="9,7" FontSize="11.5" TextColor="#5B6270" Opacity="0.45" VerticalOptions="End" />
    </VerticalStackLayout>

    <Grid Grid.Column="1" RowDefinitions="Auto,*,Auto">

      <!-- 顶部工具条 -->
      <Grid Grid.Row="0" Padding="16,12,16,10" ColumnDefinitions="Auto,*,Auto" ColumnSpacing="10">
        <Label Grid.Column="0" Text="{Binding StatusMessage}" FontSize="14" FontAttributes="Bold"
               TextColor="#1B1D22" VerticalOptions="Center" />
        <Label Grid.Column="1" Text="{Binding StatusDetail}" FontSize="10.5" TextColor="#8B93A2"
               VerticalOptions="Center" LineBreakMode="TailTruncation" />
        <Button Grid.Column="2" Text="选择文件夹" Clicked="OnChooseFolderClicked"
                BackgroundColor="#2F6FED" TextColor="#FFFFFF" FontSize="12" CornerRadius="8" Padding="14,8" />
      </Grid>

      <!-- 歌曲列表 -->
      <CollectionView Grid.Row="1" ItemsSource="{Binding Tracks}" SelectionMode="None" Margin="8,0">
        <CollectionView.ItemTemplate>
          <DataTemplate x:DataType="vm:TrackItem">
            <Grid Padding="10,0" HeightRequest="50" ColumnDefinitions="34,*,48" ColumnSpacing="11">
              <Grid.Triggers>
                <DataTrigger TargetType="Grid" Binding="{Binding IsCurrent}" Value="True">
                  <Setter Property="BackgroundColor" Value="#E7EEFC" />
                </DataTrigger>
              </Grid.Triggers>

              <Border Grid.Column="0" WidthRequest="34" HeightRequest="34" StrokeThickness="0"
                      BackgroundColor="#2F6FED" Opacity="0.45">
                <Border.StrokeShape>
                  <RoundRectangle CornerRadius="6" />
                </Border.StrokeShape>
              </Border>

              <VerticalStackLayout Grid.Column="1" VerticalOptions="Center" Spacing="1">
                <Label Text="{Binding Title}" FontSize="12" FontAttributes="Bold" TextColor="#1B1D22" LineBreakMode="TailTruncation" />
                <Label Text="{Binding Subtitle}" FontSize="10.5" TextColor="#8B93A2" LineBreakMode="TailTruncation" />
              </VerticalStackLayout>

              <Label Grid.Column="2" Text="{Binding Duration}" FontSize="11" TextColor="#8B93A2"
                     VerticalOptions="Center" HorizontalTextAlignment="End" />
            </Grid>
          </DataTemplate>
        </CollectionView.ItemTemplate>
      </CollectionView>

      <!-- 空状态 -->
      <VerticalStackLayout Grid.Row="1" IsVisible="{Binding IsEmpty}"
                           VerticalOptions="Center" HorizontalOptions="Center" Spacing="8">
        <Label Text="♫" FontSize="30" HorizontalOptions="Center" Opacity="0.3" />
        <Label Text="{Binding StatusMessage}" FontSize="13.5" FontAttributes="Bold" HorizontalOptions="Center" />
        <Label Text="{Binding StatusDetail}" FontSize="11.5" TextColor="#8B93A2" HorizontalOptions="Center" />
        <Button Text="选择文件夹" Clicked="OnChooseFolderClicked" BackgroundColor="#2F6FED"
                TextColor="#FFFFFF" FontSize="12" CornerRadius="8" Padding="18,9" HorizontalOptions="Center" />
      </VerticalStackLayout>

      <!-- 播放条占位，Task 7 填内容 -->
      <Grid Grid.Row="2" x:Name="TransportBar" HeightRequest="64" BackgroundColor="#FCFCFD" />

    </Grid>
  </Grid>
</ContentPage>
```

新建 `src/MusicPlayer.App/Views/MainPage.xaml.cs`：

```csharp
using MusicPlayer.App.ViewModels;

namespace MusicPlayer.App.Views;

public partial class MainPage : ContentPage
{
    private readonly PlayerViewModel _viewModel;

    public MainPage(PlayerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        SizeChanged += OnSizeChanged;
    }

    private async void OnChooseFolderClicked(object? sender, EventArgs e)
        => await _viewModel.ChooseFolderCommand.ExecuteAsync(null);

    /// <summary>窄窗口下把侧栏收成图标、隐藏文字（对应 spec 第 5 节）。</summary>
    private void OnSizeChanged(object? sender, EventArgs e)
    {
        var narrow = Width < 700;
        Sidebar.WidthRequest = narrow ? 48 : 132;

        foreach (var label in new[] { SidePlaying, SideLibrary, SideFolder, SidePlaylist, SideSettings })
        {
            label.Text = narrow ? label.Text.Trim()[..1] : label.Text.Trim();
        }
    }
}
```

> 窄屏处理用 `SizeChanged` 而不是 VisualStateManager：`AdaptiveTrigger` 在 Windows 上的行为不够可预期，而 `SizeChanged` 是确定性的。图标本身就只有一个字符，所以收起时取首字符即可。

- [ ] **Step 7: 接线依赖注入**

把 `src/MusicPlayer.App/MauiProgram.cs` 改成：

```csharp
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using MusicPlayer.App.ViewModels;
using MusicPlayer.App.Views;
using MusicPlayer.Core.Library;
using MusicPlayer.Core.Metadata;

namespace MusicPlayer.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<ITrackMetadataReader, TagLibMetadataReader>();
        builder.Services.AddSingleton<ILibraryScanner, FolderLibraryScanner>();
        builder.Services.AddSingleton<PlayerViewModel>();
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
```

把 `src/MusicPlayer.App/App.xaml.cs` 改成：

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace MusicPlayer.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
        => new(_services.GetRequiredService<Views.MainPage>());
}
```

- [ ] **Step 8: 跑起来手工验证**

先用 Task 9 Step 1 的脚本准备一个样本目录（里面有一个从 `E:\LocalMusic\` 复制出来的 FLAC 和一个 MP3），然后：

```powershell
dotnet run --project src/MusicPlayer.App
```

逐条确认：
- 窗口起来，标题是"音乐播放器"，右上角有最小化/最大化/关闭
- 没选文件夹时显示"还没有音乐"空状态
- 点"选择文件夹"，选那个样本目录，列表里出现两首，标题、歌手、专辑、时长、格式都对
- 选一个只有 txt 的目录，显示"这个文件夹里没有 MP3 或 FLAC"
- 把窗口拖窄到 700px 以下，侧栏收成图标

如果"选择文件夹"报错或没反应，把 `FolderPicker.Default.PickAsync` 换成 WinRT 的 `Windows.Storage.Pickers.FolderPicker`，并用 `WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd)` 传入窗口句柄。这是已知的免打包应用偶发问题。

- [ ] **Step 9: 提交**

```powershell
git add src
git commit -m "feat: MAUI 应用骨架，选文件夹、歌曲列表与空状态"
```

---

### Task 7: 接入真实播放

**Files:**
- Create: `src/MusicPlayer.App/Services/MediaElementAudioPlayer.cs`
- Modify: `src/MusicPlayer.App/ViewModels/PlayerViewModel.cs`
- Modify: `src/MusicPlayer.App/Views/MainPage.xaml` 与 `MainPage.xaml.cs`

**Interfaces:**
- Consumes: `IAudioPlayer`、`PlaybackController`、`MediaElement`
- Produces:
  - `MediaElementAudioPlayer`：构造函数接收一个 `MediaElement`
  - `PlayerViewModel.AttachPlayer(IAudioPlayer player)`、`void BeginProgressDrag()`、`void CompleteProgressDrag(double seconds)`、`void SetVolume(double value)`、命令 `TogglePlayPauseCommand` / `NextCommand` / `PreviousCommand`

- [ ] **Step 1: 实现播放器**

创建 `src/MusicPlayer.App/Services/MediaElementAudioPlayer.cs`：

```csharp
using CommunityToolkit.Maui.Views;
using MusicPlayer.Core.Playback;

namespace MusicPlayer.App.Services;

/// <summary>
/// 把 MAUI 的 MediaElement 包装成 IAudioPlayer。
/// Windows 上它走系统 Media Foundation（原生支持 FLAC），iOS 上走 AVPlayer。
/// </summary>
public sealed class MediaElementAudioPlayer : IAudioPlayer
{
    private readonly MediaElement _media;

    public MediaElementAudioPlayer(MediaElement media)
    {
        _media = media;
        _media.MediaEnded += (_, _) => Ended?.Invoke(this, EventArgs.Empty);
        _media.MediaFailed += (_, e) => Failed?.Invoke(this, e.ErrorMessage ?? "未知播放错误");
        _media.PositionChanged += (_, _) => PositionChanged?.Invoke(this, EventArgs.Empty);
        _media.StateChanged += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Ended;
    public event EventHandler? PositionChanged;
    public event EventHandler? StateChanged;
    public event EventHandler<string>? Failed;

    public TimeSpan Position => _media.Position;
    public TimeSpan Duration => _media.Duration;

    public double Volume
    {
        get => _media.Volume;
        set => _media.Volume = Math.Clamp(value, 0d, 1d);
    }

    public PlaybackState State => _media.State switch
    {
        MediaElementState.Playing => PlaybackState.Playing,
        MediaElementState.Paused => PlaybackState.Paused,
        _ => PlaybackState.Stopped
    };

    public void Load(string filePath) => _media.Source = MediaSource.FromFile(filePath);

    public void Play() => _media.Play();

    public void Pause() => _media.Pause();

    public void Stop() => _media.Stop();

    public void Seek(TimeSpan position) => _media.SeekTo(position);
}
```

- [ ] **Step 2: 把 MediaElement 放进页面**

在 `MainPage.xaml` 的 `ContentPage` 标签上加一个命名空间：

```xml
xmlns:toolkit="http://schemas.microsoft.com/dotnet/2022/maui/toolkit"
```

并把它作为 `RootGrid` 的**第一个子元素**（放在最底层，被其他元素盖住）：

```xml
<!-- 只负责出声，不参与视觉；Opacity=0 加 InputTransparent 保证看不见也点不到 -->
<toolkit:MediaElement x:Name="Media"
                      Grid.ColumnSpan="2"
                      Opacity="0"
                      InputTransparent="True"
                      ShouldAutoPlay="False"
                      ShouldShowPlaybackControls="False" />
```

- [ ] **Step 3: 填充播放条**

把 `MainPage.xaml` 里的 `<Grid Grid.Row="2" x:Name="TransportBar" ... />` 占位换成：

```xml
<Grid Grid.Row="2" x:Name="TransportBar" HeightRequest="64" BackgroundColor="#FCFCFD"
      Padding="14,0" ColumnDefinitions="190,Auto,*,96" ColumnSpacing="14">

  <HorizontalStackLayout Grid.Column="0" Spacing="9" VerticalOptions="Center">
    <Border WidthRequest="38" HeightRequest="38" StrokeThickness="0" BackgroundColor="#2F6FED">
      <Border.StrokeShape><RoundRectangle CornerRadius="7" /></Border.StrokeShape>
      <!-- 没有封面时，底下这层蓝色方块就是占位 -->
      <Image Source="{Binding NowCover}" Aspect="AspectFill" />
    </Border>
    <VerticalStackLayout VerticalOptions="Center" Spacing="0">
      <Label Text="{Binding NowTitle}" FontSize="11.5" FontAttributes="Bold" TextColor="#1B1D22" LineBreakMode="TailTruncation" />
      <Label Text="{Binding NowSubtitle}" FontSize="10" TextColor="#8B93A2" LineBreakMode="TailTruncation" />
    </VerticalStackLayout>
  </HorizontalStackLayout>

  <HorizontalStackLayout Grid.Column="1" Spacing="13" VerticalOptions="Center">
    <Button Text="⏮" Clicked="OnPreviousClicked" BackgroundColor="Transparent" TextColor="#5B6270" FontSize="14" Padding="0" WidthRequest="28" />
    <Button x:Name="PlayPauseButton" Text="▶" Clicked="OnPlayPauseClicked"
            BackgroundColor="#2F6FED" TextColor="#FFFFFF" FontSize="12"
            WidthRequest="34" HeightRequest="34" CornerRadius="17" Padding="0" />
    <Button Text="⏭" Clicked="OnNextClicked" BackgroundColor="Transparent" TextColor="#5B6270" FontSize="14" Padding="0" WidthRequest="28" />
  </HorizontalStackLayout>

  <Grid Grid.Column="2" ColumnDefinitions="Auto,*,Auto" ColumnSpacing="9" VerticalOptions="Center">
    <Label x:Name="PositionLabel" Grid.Column="0" Text="0:00" FontSize="10" TextColor="#8B93A2" VerticalOptions="Center" />
    <Slider x:Name="ProgressSlider" Grid.Column="1" Minimum="0" Maximum="1"
            MaximumTrackColor="#E2E6EE" MinimumTrackColor="#2F6FED" ThumbColor="#2F6FED"
            DragStarted="OnProgressDragStarted" DragCompleted="OnProgressDragCompleted" />
    <Label x:Name="DurationLabel" Grid.Column="2" Text="--:--" FontSize="10" TextColor="#8B93A2" VerticalOptions="Center" />
  </Grid>

  <HorizontalStackLayout Grid.Column="3" x:Name="VolumeGroup" Spacing="7" VerticalOptions="Center">
    <Label Text="🔊" FontSize="11" TextColor="#8B93A2" VerticalOptions="Center" />
    <Slider x:Name="VolumeSlider" Minimum="0" Maximum="1" Value="0.8"
            MaximumTrackColor="#E2E6EE" MinimumTrackColor="#B9C3D4" ThumbColor="#B9C3D4"
            WidthRequest="70" ValueChanged="OnVolumeChanged" />
  </HorizontalStackLayout>
</Grid>
```

最后给列表模板加上点击：在 `DataTemplate` 里那个 `<Grid Padding="10,0" ...>` 的末尾加

```xml
<Grid.GestureRecognizers>
  <TapGestureRecognizer
      Command="{Binding Source={x:Reference PageRoot}, Path=BindingContext.PlayTrackCommand}"
      CommandParameter="{Binding .}" />
</Grid.GestureRecognizers>
```

- [ ] **Step 4: ViewModel 接上播放控制器**

先在 `PlayerViewModel.cs` 顶部补一条 `using MusicPlayer.Core.Playback;`，然后补上这些成员（`PlayTrack` 的方法体也换成下面这版）：

```csharp
private PlaybackController? _controller;
private bool _isDraggingProgress;

[ObservableProperty] private string _nowTitle = string.Empty;
[ObservableProperty] private string _nowSubtitle = string.Empty;
[ObservableProperty] private double _positionSeconds;
[ObservableProperty] private double _durationSeconds = 1;
[ObservableProperty] private bool _isPlaying;
[ObservableProperty] private ImageSource? _nowCover;

/// <summary>MediaElement 必须先存在于视觉树里，所以播放器由页面构造后注入进来。</summary>
public void AttachPlayer(IAudioPlayer player)
{
    _controller = new PlaybackController(player);
    _controller.StateChanged += (_, _) => RefreshTransport();
    _controller.PositionChanged += (_, position) =>
    {
        if (!_isDraggingProgress)
        {
            PositionSeconds = position.TotalSeconds;
        }
    };
    _controller.PlaybackFailed += (_, message) =>
    {
        StatusMessage = "播放失败";
        StatusDetail = message;
    };
    _controller.Volume = 0.8;
}

[RelayCommand]
private void PlayTrack(TrackItem? item)
{
    if (item is null || _controller is null || !_controller.PlayAt(Tracks.IndexOf(item)))
    {
        return;
    }

    foreach (var track in Tracks)
    {
        track.IsCurrent = ReferenceEquals(track, item);
    }

    RefreshTransport();
}

[RelayCommand]
private void TogglePlayPause()
{
    _controller?.TogglePlayPause();
    RefreshTransport();
}

[RelayCommand]
private void Next()
{
    if (_controller?.Next() == true)
    {
        MarkCurrent();
    }

    RefreshTransport();
}

[RelayCommand]
private void Previous()
{
    if (_controller?.Previous() == true)
    {
        MarkCurrent();
    }

    RefreshTransport();
}

public void BeginProgressDrag() => _isDraggingProgress = true;

public void CompleteProgressDrag(double seconds)
{
    _isDraggingProgress = false;
    _controller?.Seek(TimeSpan.FromSeconds(seconds));
    RefreshTransport();
}

public void SetVolume(double value)
{
    if (_controller is not null)
    {
        _controller.Volume = value;
    }
}

private void MarkCurrent()
{
    var path = _controller?.Current?.FilePath;
    foreach (var track in Tracks)
    {
        track.IsCurrent = track.Track.FilePath == path;
    }
}

private void RefreshTransport()
{
    var current = _controller?.Current;
    NowTitle = current?.DisplayTitle ?? string.Empty;
    NowSubtitle = current is null ? string.Empty : $"{current.DisplayArtist} · {current.DisplayAlbum}";
    IsPlaying = _controller?.State == PlaybackState.Playing;
    DurationSeconds = Math.Max(1, _controller?.Duration.TotalSeconds ?? 1);
    PositionSeconds = _controller?.Position.TotalSeconds ?? 0;
    NowCover = current?.CoverArt is { Length: > 0 } bytes
        ? ImageSource.FromStream(() => new MemoryStream(bytes))
        : null;
}
```

别忘了解析 `ChooseFolderAsync` 里扫描完成后把队列交给控制器——在 `HasTracks = Tracks.Count > 0;` 之后加一行：

```csharp
_controller?.LoadTracks(tracks);
```

- [ ] **Step 5: 页面代码接线**

`MainPage.xaml.cs` 换成：

```csharp
using System.ComponentModel;
using MusicPlayer.App.Services;
using MusicPlayer.App.ViewModels;

namespace MusicPlayer.App.Views;

public partial class MainPage : ContentPage
{
    private readonly PlayerViewModel _viewModel;

    public MainPage(PlayerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        // MediaElement 必须先存在于视觉树里才能播放，所以播放器在这里构造再注入
        _viewModel.AttachPlayer(new MediaElementAudioPlayer(Media));

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        SizeChanged += OnSizeChanged;
    }

    private async void OnChooseFolderClicked(object? sender, EventArgs e)
        => await _viewModel.ChooseFolderCommand.ExecuteAsync(null);

    private void OnPlayPauseClicked(object? sender, EventArgs e) => _viewModel.TogglePlayPauseCommand.Execute(null);

    private void OnNextClicked(object? sender, EventArgs e) => _viewModel.NextCommand.Execute(null);

    private void OnPreviousClicked(object? sender, EventArgs e) => _viewModel.PreviousCommand.Execute(null);

    private void OnProgressDragStarted(object? sender, EventArgs e) => _viewModel.BeginProgressDrag();

    private void OnProgressDragCompleted(object? sender, EventArgs e)
        => _viewModel.CompleteProgressDrag(ProgressSlider.Value);

    private void OnVolumeChanged(object? sender, ValueChangedEventArgs e) => _viewModel.SetVolume(e.NewValue);

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerViewModel.PositionSeconds) or nameof(PlayerViewModel.DurationSeconds))
        {
            ProgressSlider.Maximum = Math.Max(1, _viewModel.DurationSeconds);
            ProgressSlider.Value = Math.Clamp(_viewModel.PositionSeconds, 0, ProgressSlider.Maximum);
            PositionLabel.Text = FormatSeconds(_viewModel.PositionSeconds);
            DurationLabel.Text = FormatSeconds(_viewModel.DurationSeconds);
        }

        if (e.PropertyName is nameof(PlayerViewModel.IsPlaying))
        {
            PlayPauseButton.Text = _viewModel.IsPlaying ? "⏸" : "▶";
        }
    }

    private static string FormatSeconds(double seconds)
        => seconds <= 0 ? "0:00" : $"{(int)(seconds / 60)}:{(int)(seconds % 60):D2}";

    /// <summary>窄窗口下把侧栏收成图标、隐藏音量（对应 spec 第 5 节）。</summary>
    private void OnSizeChanged(object? sender, EventArgs e)
    {
        var narrow = Width < 700;
        Sidebar.WidthRequest = narrow ? 48 : 132;
        VolumeGroup.IsVisible = !narrow;

        foreach (var label in new[] { SidePlaying, SideLibrary, SideFolder, SidePlaylist, SideSettings })
        {
            label.Text = narrow ? label.Text.Trim()[..1] : label.Text.Trim();
        }
    }
}
```

- [ ] **Step 6: 手工验证**

```powershell
dotnet run --project src/MusicPlayer.App
```

样本仍从 `E:\LocalMusic\` 复制出来用，**不要在源目录上操作**。逐条确认：
- 点一首歌能出声
- 播放按钮变成暂停图标，再点能暂停、能继续
- 底部条显示当前歌名、歌手、专辑
- 进度条随播放前进，拖动能跳转
- 音量滑块能改音量
- 一首播完自动播放下一首
- 播到最后一首时按"下一首"没有反应也不会崩
- 列表里当前播放那一行有浅蓝底

- [ ] **Step 7: 提交**

```powershell
git add src
git commit -m "feat: 接入 MediaElement 实现真实播放，播放条可用"
```

---

### Task 8: 窗口最小尺寸

**Files:**
- Modify: `src/MusicPlayer.App/MauiProgram.cs`

**Interfaces:**
- Consumes: 无
- Produces: 无（纯平台行为）

- [ ] **Step 1: 加 Windows 生命周期钩子**

在 `MauiProgram.cs` 顶部加 `using Microsoft.Maui.LifecycleEvents;`，并在 `builder` 那条链上加：

```csharp
#if WINDOWS
        builder.ConfigureLifecycleEvents(events =>
        {
            events.AddWindows(windows => windows.OnWindowCreated(window =>
            {
                var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                {
                    presenter.SetPreferredMinSize(new Windows.Graphics.SizeInt32(480, 520));
                }
            }));
        });
#endif
```

- [ ] **Step 2: 手工验证**

```powershell
dotnet run --project src/MusicPlayer.App
```

确认：用鼠标把窗口往小拖，到大约 480×520 就停住。此时侧栏是图标态，播放条完整可读，列表里的歌名没有被裁掉。

- [ ] **Step 3: 提交**

```powershell
git add src
git commit -m "feat: 窗口最小尺寸限制为 480x520"
```

---

### Task 9: 端到端验收

**Files:** 无新增

**Interfaces:**
- Consumes: 全部
- Produces: 一份可用的播放器

- [ ] **Step 1: 准备测试样本**

```powershell
$dest = Join-Path $env:TEMP "MusicPlayerAcceptance"
New-Item -ItemType Directory -Force -Path $dest | Out-Null
$flac = Get-ChildItem 'E:\LocalMusic' -Recurse -Filter *.flac | Select-Object -First 1
$mp3  = Get-ChildItem 'E:\LocalMusic' -Recurse -Filter *.mp3  | Select-Object -First 1
Copy-Item $flac.FullName $dest
Copy-Item $mp3.FullName  $dest
"样本目录：$dest"
"$($flac.Name) -> $([math]::Round($flac.Length/1MB,1)) MB"
"$($mp3.Name) -> $([math]::Round($mp3.Length/1MB,1)) MB"
```

全程只用 `Copy-Item`，源目录只被读取。

- [ ] **Step 2: 跑完整验收清单**

```powershell
dotnet test
dotnet run --project src/MusicPlayer.App
```

逐条打勾（对应 spec 第 8 节）：
- [ ] `dotnet run` 能起窗口
- [ ] 选上面那个样本目录，FLAC 和 MP3 都能列出，元数据正确
- [ ] 点一首能出声
- [ ] 播放/暂停、上一首、下一首、进度拖动、音量都能用
- [ ] 一首播完自动播放下一首
- [ ] 右上角最小化、最大化、关闭都能用；关闭后进程正常退出（任务管理器里确认没有残留的 `MusicPlayer.App`）
- [ ] 验收结束后 `E:\LocalMusic\` 的文件数量和修改时间没有变化

- [ ] **Step 3: 确认源目录未被改动并提交**

```powershell
(Get-ChildItem 'E:\LocalMusic' -Recurse -File).Count
git status --short
git add src tests
git commit -m "chore: PC 端最小实现完成，验收通过"
```

`E:\LocalMusic` 的文件数应当仍是 229。如果这个数字变了，立刻停下来查清是哪一步动了源目录。

---

## 已知的取舍（不是漏洞）

- **列表里不显示封面，只有播放条显示缩略图。** 列表行用蓝色圆角方块占位（这是 spec 第 5 节定的）；播放条显示文件内嵌的封面，没有封面时回退成同一个蓝色方块。
- **不做搜索。** 按 spec 第 2.2 节，搜索和记住播放位置都在后续范围。
- **失败自动跳过。** 队列里有播不了的文件会自动跳到下一首，全部失败才提示。这是 spec 第 6 节定下的行为，不喜欢就改 `PlaybackController.OnFailed` 一处。
- **没有播放模式切换。** 单曲循环、随机播放都不在 MVP 里；队列到最后一首就停。
- **扫描是同步遍历包在 `Task.Run` 里。** 两百多首文件没问题；如果以后到几万首，再改成流式增量更新列表。
- **用原生标题栏。** 没有自定义标题栏，所以没有自绘的三个窗口按钮——用的是 Windows 原生的，功能完全一致。
