using CommunityToolkit.Maui.Core;
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

    public PlaybackState State => _media.CurrentState switch
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
