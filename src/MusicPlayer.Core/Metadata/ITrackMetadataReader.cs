using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Metadata;

/// <summary>读取单个文件的元数据。实现必须永不抛异常——单个坏文件不能中断整次扫描。</summary>
public interface ITrackMetadataReader
{
    Track Read(string filePath);
}
