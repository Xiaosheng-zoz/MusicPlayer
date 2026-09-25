using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Metadata;

public sealed class TagLibMetadataReader : ITrackMetadataReader, ICoverArtReader
{
    public Track Read(string filePath)
    {
        try
        {
            // 必须写全名 TagLib.File，避免和 System.IO.File 混淆
            using var file = TagLib.File.Create(filePath);

            return new Track
            {
                FilePath = filePath,
                Title = file.Tag?.Title ?? string.Empty,
                Artist = file.Tag?.FirstPerformer ?? string.Empty,
                Album = file.Tag?.Album ?? string.Empty,
                Duration = file.Properties?.Duration ?? TimeSpan.Zero
                // 这里刻意不读封面：封面走 ICoverArtReader，等列表真正显示那一行时再读
            };
        }
        catch (Exception)
        {
            // 损坏文件、格式不符、文件不存在都会走到这里。回退到文件名即可（见 spec 第 6 节）。
            return new Track { FilePath = filePath };
        }
    }

    public byte[]? ReadCoverArt(string filePath)
    {
        try
        {
            using var file = TagLib.File.Create(filePath);
            return file.Tag?.Pictures?.FirstOrDefault()?.Data?.Data;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
