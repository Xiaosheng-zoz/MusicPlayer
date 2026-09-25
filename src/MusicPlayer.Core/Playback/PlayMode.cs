namespace MusicPlayer.Core.Playback;

public enum PlayMode
{
    /// <summary>顺序播放：一首接一首，到列表末尾停止。</summary>
    Sequential,

    /// <summary>随机播放：每次换歌随机挑一首（不会连着播同一首），也就不会停。</summary>
    Shuffle,

    /// <summary>单曲循环：一首播完从头再来；显式按上一首/下一首仍然换歌。</summary>
    RepeatOne
}
