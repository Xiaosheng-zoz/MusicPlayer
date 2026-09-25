using MusicPlayer.App.ViewModels;

namespace MusicPlayer.App.Views;

public partial class MainPage : ContentPage
{
    private readonly PlayerViewModel _viewModel;

    public MainPage(PlayerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        SizeChanged += OnSizeChanged;
    }

    private async void OnChooseFolderClicked(object? sender, EventArgs e)
        => await _viewModel.ChooseFolderCommand.ExecuteAsync(null);

    /// <summary>窄窗口下把侧栏收成图标、隐藏文字（对应 spec 第 5 节）。</summary>
    private void OnSizeChanged(object? sender, EventArgs e)
    {
        var narrow = Width < 700;
        Sidebar.WidthRequest = narrow ? 48 : 132;

        foreach (var label in new[] { SidePlaying, SideLibrary, SideFolder, SidePlaylist, SideSettings })
        {
            label.Text = narrow ? label.Text.Trim()[..1] : label.Text.Trim();
        }
    }
}
