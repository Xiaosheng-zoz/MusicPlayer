namespace IosProbe;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;

        var code = await Probe.RunAsync(text =>
        {
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            ResultLabel.Text = lines.Length > 0 ? lines[^1] : string.Empty;
            DetailLabel.Text = string.Join(Environment.NewLine, lines);
        });

        Console.Out.Flush();

        // 探针是一次性的：结果已经打印到 stdout，停两秒让真机上也能看清，然后退出让 CI 步骤结束
        await Task.Delay(2000);
        Environment.Exit(code);
    }
}
