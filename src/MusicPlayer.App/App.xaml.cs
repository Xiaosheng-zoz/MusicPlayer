using Microsoft.Extensions.DependencyInjection;

namespace MusicPlayer.App;

public partial class App : Application
{
	private readonly IServiceProvider _services;

	public App(IServiceProvider services)
	{
		InitializeComponent();
		_services = services;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(_services.GetRequiredService<Views.MainPage>())
		{
			MinimumWidth = 480,
			MinimumHeight = 520
		};

		// 直接设在窗口上：只设页面的 Title 不会传到窗口，标题栏会空着
		window.Title = $"MusicPlayer {Services.AppVersion.Display}";
		Services.DiagLog.Write($"[app] Window.Title = '{window.Title}'");

		// 标题栏左上角：图标 + 软件名 + 版本号。
		// 用 MAUI 自带的 TitleBar 控件，它会自己处理"内容延伸到标题栏"和拖拽区域，
		// 不用手写 WinRT 平台代码。
		window.TitleBar = new TitleBar
		{
			Title = $"MusicPlayer {Services.AppVersion.Display}",
			Icon = ImageSource.FromFile("applogo.png")
		};

		return window;
	}
}
