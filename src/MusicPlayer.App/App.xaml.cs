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
		=> new(_services.GetRequiredService<Views.MainPage>())
		{
			MinimumWidth = 480,
			MinimumHeight = 520
		};
}
