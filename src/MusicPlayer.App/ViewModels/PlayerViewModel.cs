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
    public partial bool IsCurrent { get; set; }
}

public sealed partial class PlayerViewModel : ObservableObject
{
    private readonly ILibraryScanner _scanner;

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
