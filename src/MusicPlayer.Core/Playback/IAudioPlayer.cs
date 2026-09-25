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
