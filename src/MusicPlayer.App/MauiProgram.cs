using Microsoft.Extensions.Logging;

using CommunityToolkit.Maui;
using MusicPlayer.App.ViewModels;
using MusicPlayer.App.Views;
using MusicPlayer.Core.Library;
using MusicPlayer.Core.Metadata;
using MusicPlayer.Core.Settings;

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

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
