using Android.App;
using Android.Content.PM;

namespace GnazzosWorld.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ResizeableActivity = true, LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
        ConfigChanges.UiMode | ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter([Android.Content.Intent.ActionView], Categories = [Android.Content.Intent.CategoryDefault, Android.Content.Intent.CategoryBrowsable], DataScheme = "gnazzosworld", DataHost = "open")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Android.OS.Bundle? savedInstanceState)
    { base.OnCreate(savedInstanceState); Receive(Intent); }
    protected override void OnNewIntent(Android.Content.Intent? intent)
    { base.OnNewIntent(intent); Receive(intent); }
    private static void Receive(Android.Content.Intent? intent)
    {
        if (intent?.GetStringExtra(GnazzosWorld.App.Notifications.InboundNotificationBridge.PayloadKey) is { } payload)
            GnazzosWorld.App.Notifications.InboundNotificationBridge.Receive(payload);
        else if (intent?.Action == Android.Content.Intent.ActionView && intent.Data?.Scheme == "gnazzosworld")
            GnazzosWorld.App.Notifications.InboundNotificationBridge.Receive(intent.DataString, directLink: true);
    }
}
