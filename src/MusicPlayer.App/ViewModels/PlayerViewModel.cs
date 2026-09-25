using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using MusicPlayer.App.Services;
using MusicPlayer.Core.Library;
using MusicPlayer.Core.Metadata;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Playback;
using MusicPlayer.Core.Settings;

namespace MusicPlayer.App.ViewModels;

public enum AppSection
{
    Music,
    Folders,
    Settings
}

public sealed partial class TrackItem : ObservableObject
{
    private readonly Func<byte[]?> _coverLoader;
    private ImageSource? _cover;
    private bool _coverLoaded;

    public TrackItem(Track track, int number, Func<byte[]?> coverLoader)
    {
        Track = track;
        Number = number;
        _coverLoader = coverLoader;
    }

    public Track Track { get; }

    /// <summary>在整张库里的序号。搜索过滤后不会重新编号，序号始终对应库里的位置。</summary>
    public int Number { get; }

    public string Title => Track.DisplayTitle;
    public string Subtitle => Track.DisplaySubtitle;
    public string Duration => Track.DisplayDuration;

    /// <summary>
    /// 列表行左侧的封面缩略图。**第一次被界面读取时才去磁盘读那一首**——
    /// CollectionView 只实例化可见的行，所以两百多首的库不会把封面全读进内存。
    /// </summary>
    public ImageSource? Cover
    {
        get
        {
            if (!_coverLoaded)
            {
                _coverLoaded = true;
                _cover = CoverImage.FromBytes(_coverLoader());
            }

            return _cover;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowColor))]
    public partial bool IsCurrent { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowColor))]
    public partial bool IsHovered { get; set; }

    /// <summary>行底色。正在播放优先于鼠标悬停，其余白色。</summary>
    public Color RowColor => IsCurrent
        ? Color.FromArgb("#E7EEFC")
        : IsHovered
            ? Color.FromArgb("#EFF3F8")
            : Colors.White;
}

public sealed partial class PlayerViewModel : ObservableObject
{
    private readonly LibraryLoader _loader;
    private readonly ICoverArtReader _coverArt;
    private readonly IAppSettingsStore _settingsStore;

    /// <summary>整张库。播放队列和序号都以它为准，搜索**不会**改动它。</summary>
    private readonly List<TrackItem> _library = new();

    private AppSettings _settings = new();
    private PlaybackController? _controller;
    private bool _isDraggingProgress;
    private bool _initializing;
    private string _nowCoverPath = string.Empty;

    public PlayerViewModel(LibraryLoader loader, ICoverArtReader coverArt, IAppSettingsStore settingsStore)
    {
        _loader = loader;
        _coverArt = coverArt;
        _settingsStore = settingsStore;

        FolderPath = string.Empty;
        SearchText = string.Empty;
        FolderStatusTitle = "还没有扫描过";
        FolderStatusDetail = "选一个音乐文件夹，或者直接把路径粘进上面的输入框";
        RefreshMusicStatus();
    }

    /// <summary>界面上显示的那一份，可能被搜索过滤过。</summary>
    public ObservableCollection<TrackItem> VisibleTracks { get; } = new();

    [ObservableProperty] public partial string FolderPath { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; }

    [ObservableProperty] public partial string FolderStatusTitle { get; set; }
    [ObservableProperty] public partial string FolderStatusDetail { get; set; }
    [ObservableProperty] public partial string MusicStatusText { get; set; }
    [ObservableProperty] public partial string MusicEmptyTitle { get; set; }
    [ObservableProperty] public partial string MusicEmptyDetail { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool HasTracks { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsBusy { get; set; }

    public bool IsEmpty => !IsBusy && VisibleTracks.Count == 0;

    // ===== 分区切换 =====

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMusicSelected))]
    [NotifyPropertyChangedFor(nameof(IsFoldersSelected))]
    [NotifyPropertyChangedFor(nameof(IsSettingsSelected))]
    public partial AppSection Section { get; set; }

    public bool IsMusicSelected => Section == AppSection.Music;
    public bool IsFoldersSelected => Section == AppSection.Folders;
    public bool IsSettingsSelected => Section == AppSection.Settings;

    [RelayCommand]
    private void ShowMusic() => Section = AppSection.Music;

    [RelayCommand]
    private void ShowFolders() => Section = AppSection.Folders;

    [RelayCommand]
    private void ShowSettings() => Section = AppSection.Settings;

    // ===== 设置 =====

    /// <summary>设置里那个开关：关掉就不再记住、也不再自动打开文件夹。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RememberLastFolderHint))]
    public partial bool RememberLastFolder { get; set; }

    public string RememberLastFolderHint => RememberLastFolder
        ? "下次启动自动打开这个文件夹"
        : "下次启动不自动打开文件夹，已记住的路径也清掉了";

    public string SettingsFilePath => $"设置文件：{_settingsStore.Location}";

    [RelayCommand]
    private async Task InitializeAsync()
    {
        _initializing = true;
        try
        {
            _settings = _settingsStore.Load();
            RememberLastFolder = _settings.RememberLastFolder;

            if (_settings.RememberLastFolder && !string.IsNullOrWhiteSpace(_settings.LastFolder))
            {
                FolderPath = _settings.LastFolder;
                await LoadFolderAsync(_settings.LastFolder);
            }
        }
        finally
        {
            _initializing = false;
        }
    }

    partial void OnRememberLastFolderChanged(bool value)
    {
        if (_initializing)
        {
            return; // 初始化时是在读设置，不要立刻回写
        }

        _settings = _settings with
        {
            RememberLastFolder = value,
            // 关掉就把记住的路径一并清掉：留着一条"已经说过不要记"的路径不合理
            LastFolder = value && HasTracks ? FolderPath : null
        };

        _settingsStore.Save(_settings);
    }

    // ===== 扫描文件夹 =====

    [RelayCommand]
    private async Task ChooseFolderAsync()
    {
        DiagLog.Write("PickAsync: start");

        FolderPickerResult picked;
        try
        {
            picked = await FolderPicker.Default.PickAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            DiagLog.Write($"PickAsync THREW: {ex}");
            FolderStatusTitle = "选择文件夹出错";
            FolderStatusDetail = ex.Message;
            return;
        }

        DiagLog.Write($"PickAsync: IsSuccessful={picked.IsSuccessful}, "
                    + $"Folder={picked.Folder?.Path ?? "<null>"}, "
                    + $"Exception={picked.Exception?.Message ?? "<none>"}");

        if (!picked.IsSuccessful || picked.Folder is null)
        {
            // 不能静默返回：用户要么是放弃了，要么是没能让对话框里的确认按钮亮起来，
            // 两种都需要看到一句人话，否则界面看起来像坏了。
            FolderStatusTitle = "没有选到文件夹";
            FolderStatusDetail = picked.Exception?.Message ?? "对话框没有返回文件夹";
            return;
        }

        FolderPath = picked.Folder.Path;
        await LoadFolderAsync(FolderPath);
    }

    /// <summary>打开输入框里那个路径。</summary>
    [RelayCommand]
    private async Task OpenFolderAsync() => await LoadFolderAsync(FolderPath);

    private async Task LoadFolderAsync(string? path)
    {
        IsBusy = true;
        FolderStatusTitle = "正在扫描…";
        FolderStatusDetail = path ?? string.Empty;

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await _loader.LoadAsync(path);
            stopwatch.Stop();
            DiagLog.Write($"[vm] Load: path={path}, count={result.Tracks.Count}, "
                        + $"elapsed={stopwatch.ElapsedMilliseconds} ms, msg={result.StatusMessage}");

            _library.Clear();
            foreach (var track in result.Tracks)
            {
                _library.Add(new TrackItem(track, _library.Count + 1, () => _coverArt.ReadCoverArt(track.FilePath)));
            }

            _controller?.LoadTracks(result.Tracks);
            HasTracks = _library.Count > 0;

            // 文件夹那一栏只报数量，列表在「音乐」里
            FolderStatusTitle = result.StatusMessage;
            FolderStatusDetail = result.StatusDetail;

            ApplyFilter();

            // 只记住真实存在的文件夹：路径打错不该被记下来，否则下次启动就自动报错
            if (result.FolderExists && path is not null)
            {
                FolderPath = path; // 顺便把首尾空格去掉

                if (_settings.RememberLastFolder)
                {
                    _settings = _settings with { LastFolder = path };
                    _settingsStore.Save(_settings);
                }
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ===== 搜索 =====

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        VisibleTracks.Clear();
        foreach (var item in _library)
        {
            if (TrackSearch.Matches(item.Track, SearchText))
            {
                VisibleTracks.Add(item);
            }
        }

        OnPropertyChanged(nameof(IsEmpty));
        RefreshMusicStatus();
    }

    private void RefreshMusicStatus()
    {
        if (!HasTracks)
        {
            MusicStatusText = "还没有音乐";
            MusicEmptyTitle = "还没有音乐";
            MusicEmptyDetail = "到左边的「文件夹」里选一个文件夹";
            return;
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            MusicStatusText = $"找到 {VisibleTracks.Count} 首";
            MusicEmptyTitle = "没有匹配的歌";
            MusicEmptyDetail = $"没有歌名或歌手包含「{SearchText.Trim()}」";
            return;
        }

        MusicStatusText = $"{_library.Count} 首";
        MusicEmptyTitle = string.Empty;
        MusicEmptyDetail = string.Empty;
    }

    // ===== 播放 =====

    [ObservableProperty] public partial string NowTitle { get; set; }
    [ObservableProperty] public partial string NowSubtitle { get; set; }
    [ObservableProperty] public partial ImageSource? NowCover { get; set; }
    [ObservableProperty] public partial double PositionSeconds { get; set; }
    [ObservableProperty] public partial double DurationSeconds { get; set; }
    [ObservableProperty] public partial bool IsPlaying { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayModeGlyph))]
    [NotifyPropertyChangedFor(nameof(PlayModeTooltip))]
    public partial PlayMode Mode { get; set; }

    public string PlayModeGlyph => Mode switch
    {
        PlayMode.Shuffle => "🔀",
        PlayMode.RepeatOne => "🔂",
        _ => "➡️"
    };

    public string PlayModeTooltip => Mode switch
    {
        PlayMode.Shuffle => "随机播放 · 点击切换到单曲循环",
        PlayMode.RepeatOne => "单曲循环 · 点击切换到顺序播放",
        _ => "顺序播放 · 点击切换到随机播放"
    };

    /// <summary>MediaElement 必须先存在于视觉树里，所以播放器由页面构造好再注入进来。</summary>
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
        _controller.PlaybackFailed += (_, message) => MusicStatusText = $"播放失败：{message}";
        _controller.Volume = 0.8;
        _controller.Mode = Mode;
    }

    [RelayCommand]
    private void PlayTrack(TrackItem? item)
    {
        // 注意：索引用的是整张库（_library），不是搜索后的 VisibleTracks，
        // 否则搜到第 3 条、点它，播的会是队列里的第 3 首而不是它。
        if (item is null || _controller is null || !_controller.PlayAt(_library.IndexOf(item)))
        {
            return;
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
        _controller?.Next();
        RefreshTransport();
    }

    [RelayCommand]
    private void Previous()
    {
        _controller?.Previous();
        RefreshTransport();
    }

    /// <summary>顺序 → 随机 → 单曲 → 顺序，循环切换。</summary>
    [RelayCommand]
    private void CyclePlayMode()
    {
        Mode = Mode switch
        {
            PlayMode.Sequential => PlayMode.Shuffle,
            PlayMode.Shuffle => PlayMode.RepeatOne,
            _ => PlayMode.Sequential
        };

        if (_controller is not null)
        {
            _controller.Mode = Mode;
        }
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
        foreach (var track in _library)
        {
            track.IsCurrent = track.Track.FilePath == path;
        }
    }

    private void RefreshTransport()
    {
        var current = _controller?.Current;
        var durationSeconds = Math.Max(1, _controller?.Duration.TotalSeconds ?? 1);

        DiagLog.Write($"[vm] RefreshTransport state={_controller?.State}, "
                    + $"dur={_controller?.Duration}, durSeconds={durationSeconds}, "
                    + $"title={current?.DisplayTitle ?? "<null>"}");

        NowTitle = current?.DisplayTitle ?? string.Empty;
        // 播放条只显示歌名和歌手，不显示专辑
        NowSubtitle = current?.DisplayArtist ?? string.Empty;
        IsPlaying = _controller?.State == PlaybackState.Playing;
        DurationSeconds = durationSeconds;
        PositionSeconds = _controller?.Position.TotalSeconds ?? 0;

        // 播放条封面也按需读，并且只在换歌时才读一次
        var coverPath = current?.FilePath ?? string.Empty;
        if (_nowCoverPath != coverPath)
        {
            _nowCoverPath = coverPath;
            NowCover = coverPath.Length == 0 ? null : CoverImage.FromBytes(_coverArt.ReadCoverArt(coverPath));
        }

        // 放在这里而不是只在上一首/下一首命令里：自动切歌也走这条路，
        // 否则播完自动下一首时列表高亮会停在上一首。
        MarkCurrent();
    }
}
