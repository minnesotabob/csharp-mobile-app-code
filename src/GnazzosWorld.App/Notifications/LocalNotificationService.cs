using GnazzosWorld.Core.Notifications;
#if ANDROID
using Android.App;
using Android.Content;
using AndroidX.Core.App;
#elif IOS
using Foundation;
using UserNotifications;
#endif

namespace GnazzosWorld.App.Notifications;

public sealed class LocalNotificationService(TimeProvider clock)
{
    public async Task<bool> RequestPermissionAsync()
    {
#if ANDROID
        var permission = await Permissions.RequestAsync<NotificationPermission>();
        using var compat = NotificationManagerCompat.From(Platform.AppContext);
        return permission == PermissionStatus.Granted && compat?.AreNotificationsEnabled() == true;
#elif IOS
        var result = await UNUserNotificationCenter.Current.RequestAuthorizationAsync(UNAuthorizationOptions.Alert);
        return result.Item1;
#else
        await Task.CompletedTask; return false;
#endif
    }
    public async Task<bool> ShowPublicSampleAsync(string catalogId)
    {
        var target = LinkTarget.Parse(LinkTarget.PublicShare(catalogId))!;
        var payload = NotificationEnvelope.Local(target, clock.GetUtcNow()).Encode();
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(33) && await Permissions.CheckStatusAsync<NotificationPermission>() != PermissionStatus.Granted) return false;
        var context = Platform.AppContext;
        using var compat = NotificationManagerCompat.From(context);
        if (compat is null || !compat.AreNotificationsEnabled()) return false;
        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (manager is null) return false;
        const string channel = "gnazzo-public-updates-v1";
        using var definition = new NotificationChannel(channel, "Public catalog lab updates", NotificationImportance.Default);
        manager.CreateNotificationChannel(definition);
        if (manager.GetNotificationChannel(channel)?.Importance == NotificationImportance.None) return false;
        using var intent = new Intent(context, typeof(MainActivity));
        intent.SetAction("gnazzosworld.NOTIFICATION");
        intent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        intent.PutExtra(InboundNotificationBridge.PayloadKey, payload);
        using var pending = PendingIntent.GetActivity(context, 3001, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)
            ?? throw new InvalidOperationException("Notification action could not be created.");
        using var builder = new NotificationCompat.Builder(context, channel);
        builder.SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo);
        builder.SetContentTitle("Gnazzo's World public catalog");
        builder.SetContentText("A public item is ready to explore. Tap to open the app.");
        builder.SetAutoCancel(true); builder.SetContentIntent(pending);
        using var notification = builder.Build() ?? throw new InvalidOperationException("Notification could not be built.");
        compat.Notify(3001, notification); return true; // Posted is not proof of display or a tap.
#elif IOS
        var settings = await UNUserNotificationCenter.Current.GetNotificationSettingsAsync();
        if (settings.AuthorizationStatus != UNAuthorizationStatus.Authorized) return false;
        using var content = new UNMutableNotificationContent
        {
            Title = "Gnazzo's World public catalog", Body = "A public item is ready to explore. Tap to open the app.",
            UserInfo = NSDictionary.FromObjectAndKey(new NSString(payload), new NSString(InboundNotificationBridge.PayloadKey))
        };
        using var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(1, false);
        using var request = UNNotificationRequest.FromIdentifier("gnazzo-public-lab", content, trigger);
        await UNUserNotificationCenter.Current.AddNotificationRequestAsync(request); return true;
#else
        await Task.CompletedTask; return false;
#endif
    }
#if ANDROID
    public sealed class NotificationPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            OperatingSystem.IsAndroidVersionAtLeast(33) ? [(global::Android.Manifest.Permission.PostNotifications, true)] : [];
    }
#endif
}
#if IOS
public sealed class LocalNotificationReceiver : UNUserNotificationCenterDelegate
{
    public override void WillPresentNotification(UNUserNotificationCenter center, UNNotification notification,
        Action<UNNotificationPresentationOptions> completionHandler) => completionHandler(UNNotificationPresentationOptions.Banner);
    public override void DidReceiveNotificationResponse(UNUserNotificationCenter center, UNNotificationResponse response, Action completionHandler)
    {
        try
        {
            if (response.IsDefaultAction)
                InboundNotificationBridge.Receive(response.Notification.Request.Content.UserInfo[new NSString(InboundNotificationBridge.PayloadKey)]?.ToString());
        }
        finally { completionHandler(); }
    }
}
#endif
