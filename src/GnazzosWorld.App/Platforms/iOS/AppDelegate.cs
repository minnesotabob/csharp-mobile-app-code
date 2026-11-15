using Foundation;

namespace GnazzosWorld.App;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    private readonly GnazzosWorld.App.Notifications.LocalNotificationReceiver notifications = new();
    public override bool FinishedLaunching(UIKit.UIApplication application, NSDictionary launchOptions)
    {
        UserNotifications.UNUserNotificationCenter.Current.Delegate = notifications;
        return base.FinishedLaunching(application, launchOptions);
    }
    public override bool OpenUrl(UIKit.UIApplication application, NSUrl url, NSDictionary options)
    {
        if (url.Scheme == "gnazzosworld")
        { GnazzosWorld.App.Notifications.InboundNotificationBridge.Receive(url.AbsoluteString, directLink: true); return true; }
        return base.OpenUrl(application, url, options); // Preserve the identity SDK callback path.
    }
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
