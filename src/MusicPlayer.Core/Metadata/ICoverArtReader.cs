namespace MusicPlayer.Core.Metadata;

/// <summary>
/// 按需读取内嵌封面。单独抽出来是为了让扫描阶段完全不碰封面数据——
/// 一个两百多首的库，封面字节加起来能到几百 MB，扫描时读它们既没必要也浪费内存。
/// </summary>
public interface ICoverArtReader
{
    /// <summary>返回内嵌封面的原始字节；没有封面或读不出来时返回 null。</summary>
    byte[]? ReadCoverArt(string filePath);
}
