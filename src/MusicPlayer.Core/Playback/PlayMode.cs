namespace MusicPlayer.Core.Playback;

public enum PlayMode
{
    /// <summary>顺序播放（列表循环）：一首接一首，放完最后一首回到第一首继续，不会停。</summary>
    Sequential,

    /// <summary>随机播放：整张列表打乱后逐首放完，放完再重新打乱；每一轮里每首歌只出现一次。</summary>
    Shuffle,

    /// <summary>单曲循环：一首播完从头再来；显式按上一首/下一首仍然换歌。</summary>
    RepeatOne
}
