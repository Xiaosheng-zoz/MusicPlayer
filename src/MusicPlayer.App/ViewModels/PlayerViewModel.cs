using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
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

    [ObservableProperty]
    public partial bool IsCurrent { get; set; }
}

public sealed partial class PlayerViewModel : ObservableObject
{
    private readonly ILibraryScanner _scanner;
    private PlaybackController? _controller;
    private bool _isDraggingProgress;

    public PlayerViewModel(ILibraryScanner scanner)
    {
        _scanner = scanner;
        StatusMessage = "还没有音乐";
        StatusDetail = "选一个存放音乐的文件夹，支持 MP3 和 FLAC";
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

            _controller?.LoadTracks(tracks);
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
}
