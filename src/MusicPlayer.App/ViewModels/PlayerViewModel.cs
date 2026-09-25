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

namespace MusicPlayer.App.ViewModels;

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

    /// <summary>行底色。正在播放优先于鼠标悬停，其余透明。</summary>
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
    private PlaybackController? _controller;
    private bool _isDraggingProgress;
    private string _nowCoverPath = string.Empty;

    public PlayerViewModel(LibraryLoader loader, ICoverArtReader coverArt)
    {
        _loader = loader;
        _coverArt = coverArt;
        FolderPath = string.Empty;
        StatusMessage = "还没有音乐";
        StatusDetail = "点「浏览」选一个文件夹，或直接把路径粘贴到输入框里";
    }

    public ObservableCollection<TrackItem> Tracks { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool HasTracks { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty] public partial string StatusMessage { get; set; }
    [ObservableProperty] public partial string StatusDetail { get; set; }
    [ObservableProperty] public partial string FolderPath { get; set; }

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

    public bool IsEmpty => !HasTracks && !IsBusy;

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
        _controller.PlaybackFailed += (_, message) =>
        {
            StatusMessage = "播放失败";
            StatusDetail = message;
        };
        _controller.Volume = 0.8;
        _controller.Mode = Mode;
    }

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
            StatusMessage = "选择文件夹出错";
            StatusDetail = ex.Message;
            return;
        }

        DiagLog.Write($"PickAsync: IsSuccessful={picked.IsSuccessful}, "
           + $"Folder={picked.Folder?.Path ?? "<null>"}, "
           + $"Exception={picked.Exception?.Message ?? "<none>"}, "
           + $"ExceptionType={picked.Exception?.GetType().FullName ?? "<none>"}");

        if (!picked.IsSuccessful || picked.Folder is null)
        {
            // 不能静默返回：用户要么是放弃了，要么是没能让确认按钮亮起来，
            // 两种都需要看到一句人话，否则界面看起来像坏了。
            StatusMessage = "没有选到文件夹";
            StatusDetail = picked.Exception?.Message ?? "对话框没有返回文件夹";
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
        StatusMessage = "正在扫描…";
        StatusDetail = path ?? string.Empty;

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await _loader.LoadAsync(path);
            stopwatch.Stop();
            DiagLog.Write($"[vm] Load: path={path}, count={result.Tracks.Count}, "
                        + $"elapsed={stopwatch.ElapsedMilliseconds} ms, msg={result.StatusMessage}");

            Tracks.Clear();
            foreach (var track in result.Tracks)
            {
                Tracks.Add(new TrackItem(track, Tracks.Count + 1, () => _coverArt.ReadCoverArt(track.FilePath)));
            }

            _controller?.LoadTracks(result.Tracks);
            HasTracks = Tracks.Count > 0;
            StatusMessage = result.StatusMessage;
            StatusDetail = result.StatusDetail;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void PlayTrack(TrackItem? item)
    {
        if (item is null || _controller is null || !_controller.PlayAt(Tracks.IndexOf(item)))
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
        var durationSeconds = Math.Max(1, _controller?.Duration.TotalSeconds ?? 1);
        DiagLog.Write($"[vm] RefreshTransport state={_controller?.State}, "
                    + $"dur={_controller?.Duration}, durSeconds={durationSeconds}, "
                    + $"field DurationSeconds={DurationSeconds}, "
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
