using UIKit;

namespace IosProbe;

public sealed class ProbeViewController : UIViewController
{
    private readonly UITextView _text = new();

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        View!.BackgroundColor = UIColor.White;

        _text.Frame = View.Bounds;
        _text.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
        _text.Editable = false;
        _text.Font = UIFont.SystemFontOfSize(11);

        View.AddSubview(_text);
    }

    public override async void ViewDidAppear(bool animated)
    {
        base.ViewDidAppear(animated);

        var code = await Probe.RunAsync(text => _text.Text = text);

        Console.Out.Flush();

        // 一次性探针：结果已写到 stdout（CI 日志），停一会儿让真机上也能看清，然后退出
        await Task.Delay(1500);
        Environment.Exit(code);
    }
}
