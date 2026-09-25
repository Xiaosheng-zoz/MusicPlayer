using Microsoft.Extensions.Logging;

using CommunityToolkit.Maui;
using MusicPlayer.App.ViewModels;
using MusicPlayer.App.Views;
using MusicPlayer.Core.Library;
using MusicPlayer.Core.Metadata;
using MusicPlayer.Core.Settings;
using Microsoft.Maui.LifecycleEvents;

namespace MusicPlayer.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.UseMauiCommunityToolkitMediaElement()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<TagLibMetadataReader>();
		builder.Services.AddSingleton<ITrackMetadataReader>(sp => sp.GetRequiredService<TagLibMetadataReader>());
		builder.Services.AddSingleton<ICoverArtReader>(sp => sp.GetRequiredService<TagLibMetadataReader>());
		builder.Services.AddSingleton<ILibraryScanner, FolderLibraryScanner>();
		builder.Services.AddSingleton<LibraryLoader>();
		builder.Services.AddSingleton<PlayerViewModel>();
		builder.Services.AddSingleton<MainPage>();

		var settingsPath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"MusicPlayer",
			"settings.json");
		builder.Services.AddSingleton<IAppSettingsStore>(new JsonAppSettingsStore(settingsPath));

#if WINDOWS
		// 标题栏左上角的图标：不显式设的话 WinUI 不会自己从 exe 里取
		builder.ConfigureLifecycleEvents(events =>
		{
			events.AddWindows(windows => windows.OnWindowCreated(window =>
			{
				var iconPath = Path.Combine(AppContext.BaseDirectory, "appicon.ico");
				if (!File.Exists(iconPath))
				{
					Services.DiagLog.Write($"[app] 找不到图标文件：{iconPath}");
					return;
				}

				var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
				var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
				Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId).SetIcon(iconPath);
			}));
		});
#endif

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
