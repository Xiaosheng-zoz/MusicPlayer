using System.ComponentModel;
using MusicPlayer.App.Services;
using MusicPlayer.App.ViewModels;

namespace MusicPlayer.App.Views;

public partial class MainPage : ContentPage
{
    private readonly PlayerViewModel _viewModel;

    public MainPage(PlayerViewModel viewModel)
    {
        // 必须先赋值：XAML 里音量滑块的初始 Value 会立刻触发 ValueChanged
        _viewModel = viewModel;
        InitializeComponent();
        BindingContext = _viewModel;

        // MediaElement 必须先存在于视觉树里才能播放，所以播放器在这里构造再注入
        _viewModel.AttachPlayer(new MediaElementAudioPlayer(Media));

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        SizeChanged += OnSizeChanged;

        Loaded += async (_, _) => await OnPageLoadedAsync();
    }

    private async Task OnPageLoadedAsync()
    {
#if WINDOWS
        HookSpaceKey();
#endif
        await _viewModel.InitializeCommand.ExecuteAsync(null);
    }

    private async void OnChooseFolderClicked(object? sender, EventArgs e)
        => await _viewModel.ChooseFolderCommand.ExecuteAsync(null);

    private async void OnFolderPathCompleted(object? sender, EventArgs e)
        => await _viewModel.OpenFolderCommand.ExecuteAsync(null);

    private void OnPlayPauseClicked(object? sender, EventArgs e) => _viewModel.TogglePlayPauseCommand.Execute(null);

    private void OnNextClicked(object? sender, EventArgs e) => _viewModel.NextCommand.Execute(null);

    private void OnPreviousClicked(object? sender, EventArgs e) => _viewModel.PreviousCommand.Execute(null);

    private void OnProgressDragStarted(object? sender, EventArgs e) => _viewModel.BeginProgressDrag();

    private void OnProgressDragCompleted(object? sender, EventArgs e)
        => _viewModel.CompleteProgressDrag(ProgressSlider.Value);

    private void OnVolumeChanged(object? sender, ValueChangedEventArgs e) => _viewModel.SetVolume(e.NewValue);

    private void OnPlayModeClicked(object? sender, EventArgs e) => _viewModel.CyclePlayModeCommand.Execute(null);

#if WINDOWS
    /// <summary>把空格键接到播放/暂停上。</summary>
    private void HookSpaceKey()
    {
        if (Window?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window nativeWindow)
        {
            return;
        }

        if (nativeWindow.Content is not Microsoft.UI.Xaml.UIElement root)
        {
            return;
        }

        root.PreviewKeyDown -= OnNativePreviewKeyDown;
        root.PreviewKeyDown += OnNativePreviewKeyDown;
    }

    private void OnNativePreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Space)
        {
            return;
        }

        // 在输入框里空格得照常输入，不能被抢走
        if (sender is Microsoft.UI.Xaml.FrameworkElement element
            && Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(element.XamlRoot)
                is Microsoft.UI.Xaml.Controls.TextBox)
        {
            return;
        }

        _viewModel.TogglePlayPauseCommand.Execute(null);
        e.Handled = true;
    }
#endif

    private void OnRowPointerEntered(object? sender, PointerEventArgs e) => SetRowHovered(sender, true);

    private void OnRowPointerExited(object? sender, PointerEventArgs e) => SetRowHovered(sender, false);

    /// <summary>手势识别器的 BindingContext 继承自它所在的那一行。</summary>
    private static void SetRowHovered(object? sender, bool hovered)
    {
        var item = (sender as BindableObject)?.BindingContext as TrackItem
                   ?? (sender as Element)?.Parent?.BindingContext as TrackItem;

        if (item is not null)
        {
            item.IsHovered = hovered;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        try
        {
            ApplyTransportToUi(e);
        }
        catch (Exception ex)
        {
            DiagLog.Write($"[page] EXCEPTION on {e.PropertyName}: {ex}");
        }
    }

    private void ApplyTransportToUi(PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerViewModel.PositionSeconds) or nameof(PlayerViewModel.DurationSeconds))
        {
            ProgressSlider.Maximum = Math.Max(1, _viewModel.DurationSeconds);
            ProgressSlider.Value = Math.Clamp(_viewModel.PositionSeconds, 0, ProgressSlider.Maximum);
            PositionLabel.Text = FormatSeconds(_viewModel.PositionSeconds);
            DurationLabel.Text = FormatSeconds(_viewModel.DurationSeconds);
        }

        if (e.PropertyName is nameof(PlayerViewModel.IsPlaying))
        {
            // 不要用 ⏸(U+23F8)：它默认走表情符号字体，会被画进一块彩色圆角底板里。
            PlayPauseButton.Text = _viewModel.IsPlaying ? "▮▮" : "▶";
        }
    }

    private static string FormatSeconds(double seconds)
        => seconds <= 0 ? "0:00" : $"{(int)(seconds / 60)}:{(int)(seconds % 60):D2}";

    /// <summary>窄窗口下把侧栏收成图标、隐藏音量（对应 spec 第 5 节）。</summary>
    private void OnSizeChanged(object? sender, EventArgs e)
    {
        var narrow = Width < 700;
        Sidebar.WidthRequest = narrow ? 48 : 132;
        VolumeGroup.IsVisible = !narrow;

        foreach (var label in new[] { SidePlaying, SideLibrary, SideFolder, SidePlaylist, SideSettings })
        {
            label.Text = narrow ? label.Text.Trim()[..1] : label.Text.Trim();
        }
    }
}
