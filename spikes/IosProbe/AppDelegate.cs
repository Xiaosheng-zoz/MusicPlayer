using Foundation;
using UIKit;

namespace IosProbe;

[Register(nameof(AppDelegate))]
public sealed class AppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        Window = new UIWindow(UIScreen.MainScreen.Bounds)
        {
            RootViewController = new ProbeViewController()
        };

        Window.MakeKeyAndVisible();
        return true;
    }
}
