using System.Reflection;

namespace MusicPlayer.App.Services;

/// <summary>
/// 版本号的唯一来源是 csproj 里的 &lt;Version&gt;，它会写进
/// AssemblyInformationalVersion（形如 1.1.0+commit哈希）。
/// 这里只取前面的版本部分。
///
/// 不用 AppInfo.Current.VersionString：它读的是 MAUI 在构建时烧进程序集的
/// 元数据，实测会读到过期值（显示 1.0.0.1 而实际是 1.1.0）。
/// </summary>
internal static class AppVersion
{
    public static string Display { get; } = Read();

    private static string Read()
    {
        var informational = typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            return "0.0.0";
        }

        var plus = informational.IndexOf('+');
        return plus < 0 ? informational : informational[..plus];
    }
}
