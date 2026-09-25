using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using MusicPlayer.App.Services;
using MusicPlayer.Core.Library;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Playback;

namespace MusicPlayer.App.ViewModels;

public sealed partial class TrackItem : ObservableObject
{
    public TrackItem(Track track) => Track = track;

    public Track Track { get; }
    public string Title => Track.DisplayTitle;
    public string Subtitle => Track.DisplaySubtitle;
    public string Duration => Track.DisplayDuration;

    /// <summary>列表行左侧的封面缩略图。没有内嵌封面时为 null，由 XAML 里的底色兜底。</summary>
    public ImageSource? Cover => Track.CoverArt is { Length: > 0 } bytes
        ? ImageSource.FromStream(() => new MemoryStream(bytes))
        : null;

    [ObservableProperty]
    public partial bool IsCurrent { get; set; }
}

public sealed partial class PlayerViewModel : ObservableObject
{
    private readonly LibraryLoader _loader;
    private PlaybackController? _controller;
    private bool _isDraggingProgress;

    public PlayerViewModel(LibraryLoader loader)
    {
        _loader = loader;
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
            var result = await _loader.LoadAsync(path);
            DiagLog.Write($"Load: path={path}, count={result.Tracks.Count}, msg={result.StatusMessage}");

            Tracks.Clear();
            foreach (var track in result.Tracks)
            {
                Tracks.Add(new TrackItem(track));
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
        var durationSeconds = Math.Max(1, _controller?.Duration.TotalSeconds ?? 1);
        DiagLog.Write($"[vm] RefreshTransport state={_controller?.State}, "
                    + $"dur={_controller?.Duration}, durSeconds={durationSeconds}, "
                    + $"field DurationSeconds={DurationSeconds}, "
                    + $"title={current?.DisplayTitle ?? "<null>"}");
        NowTitle = current?.DisplayTitle ?? string.Empty;
        NowSubtitle = current is null ? string.Empty : $"{current.DisplayArtist} · {current.DisplayAlbum}";
        IsPlaying = _controller?.State == PlaybackState.Playing;
        DurationSeconds = durationSeconds;
        PositionSeconds = _controller?.Position.TotalSeconds ?? 0;
        NowCover = current?.CoverArt is { Length: > 0 } bytes
            ? ImageSource.FromStream(() => new MemoryStream(bytes))
            : null;
    }
}
