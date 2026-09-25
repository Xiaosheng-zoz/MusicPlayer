using System.Text;
using AVFoundation;
using Foundation;
using UIKit;

namespace IosProbe;

/// <summary>
/// 一次性探针：验证 iOS 能不能读、能不能播我们的 24bit / 96kHz FLAC。
/// 结果同时写进 Console（CI 日志）和界面（真机上肉眼可见）。
/// </summary>
public static class Probe
{
    private const string SampleName = "sample-24bit96k.flac";

    public static async Task<int> RunAsync(Action<string> show)
    {
        var text = new StringBuilder();

        void W(string line = "")
        {
            text.AppendLine(line);
            show(text.ToString());
            Console.WriteLine(line);
        }

        W("=== iOS 音频探针 ===");
        W($"设备: {UIDevice.CurrentDevice.Model} / iOS {UIDevice.CurrentDevice.SystemVersion}");

        var passed = true;

        // 1. 把随包样本解到临时目录：TagLib 和 AVPlayer 都需要真实文件路径
        string samplePath;
        try
        {
            var bundled = NSBundle.MainBundle.PathForResource("sample-24bit96k", "flac");
            if (bundled is null)
            {
                W("[1] 失败：应用包里找不到 sample-24bit96k.flac");
                W("结论: FAIL");
                return 1;
            }

            samplePath = Path.Combine(Path.GetTempPath(), SampleName);
            File.Copy(bundled, samplePath, overwrite: true);
            W($"[1] 随包样本已解出 {new FileInfo(samplePath).Length / 1024.0 / 1024.0:F2} MB");
        }
        catch (Exception ex)
        {
            W($"[1] 失败：解出样本出错 {ex.GetType().Name}: {ex.Message}");
            W("结论: FAIL");
            return 1;
        }

        // 2. 元数据：和 PC 端用同一个库
        try
        {
            using var file = TagLib.File.Create(samplePath);
            W("[2] TagLib 读取成功");
            W($"      标题={file.Tag?.Title}  歌手={file.Tag?.FirstPerformer}");
            W($"      时长={file.Properties?.Duration}  采样率={file.Properties?.AudioSampleRate}"
            + $"  位深={file.Properties?.BitsPerSample}  声道={file.Properties?.AudioChannels}");
            W($"      封面字节数={file.Tag?.Pictures?.FirstOrDefault()?.Data?.Count ?? 0}");
        }
        catch (Exception ex)
        {
            passed = false;
            W($"[2] 失败：TagLib 读取出错 {ex.GetType().Name}: {ex.Message}");
        }

        // 3. 播放：唯一有说服力的判据是"播放时间有没有推进"——
        //    文件被接受（ReadyToPlay）不等于真的解码出了声音。
        try
        {
            var player = new AVPlayer(NSUrl.FromFilename(samplePath));
            player.Play();
            await Task.Delay(4000);

            // 注意：.NET for iOS 把原生的 -(CMTime)currentTime 绑定成了属性，不是方法
            var position = player.CurrentTime.Seconds;
            var item = player.CurrentItem;

            W("[3] AVFoundation 播放");
            W($"      item.Status={item?.Status}  player.Rate={player.Rate}");
            W($"      播放位置推进到 {position:F2} 秒");

            if (item?.Error is not null)
            {
                W($"      item.Error={item.Error.LocalizedDescription}");
            }

            if (position > 0.5)
            {
                W("      结论：真的在解码播放");
            }
            else
            {
                passed = false;
                W("      失败：播放位置没有推进，解码没成功");
            }

            player.Pause();
        }
        catch (Exception ex)
        {
            passed = false;
            W($"[3] 失败：播放抛异常 {ex.GetType().Name}: {ex.Message}");
        }

        W(passed ? "结论: PASS" : "结论: FAIL");
        return passed ? 0 : 1;
    }
}
