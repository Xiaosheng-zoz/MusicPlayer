namespace MusicPlayer.App.Services;

/// <summary>
/// 极简诊断日志：把关键状态变化追加到临时目录的一个文件里。
/// 自用工具没有遥测，出问题时这是唯一的现场记录。
/// </summary>
public static class DiagLog
{
    public static void Write(string message)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(Path.GetTempPath(), "musicplayer-diag.log"),
                $"{DateTime.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}");
        }
        catch
        {
            // 诊断失败不能影响正常流程
        }
    }
}
