using GnazzosWorld.App.Notifications;
using GnazzosWorld.Core.Notifications;

namespace GnazzosWorld.App.Pages;

public sealed class NotificationLabPage : ContentPage
{
    private const string PublicId = "song-minnesota-bob-northern-lights";
    public NotificationLabPage(LocalNotificationService local, NotificationRouter router)
    {
        Title = "Notification and link lab";
        var status = new Label(); var input = new Entry { Text = LinkTarget.PublicShare(PublicId), Placeholder = "Canonical lab link" };
        var permission = new Button { Text = "Choose notification permission" };
        var show = new Button { Text = "Post one public local notification" };
        var route = new Button { Text = "Validate and open this lab link" };
        var share = new Button { Text = "Share public catalog lab link" };
        var retry = new Button { Text = "Retry pending navigation" };
        var clear = new Button { Text = "Discard pending links and receipts" };
        permission.Clicked += async (_, _) => await Run(async () => status.Text = await local.RequestPermissionAsync() ? "Permission currently granted; delivery remains a device observation." : "Permission unavailable or denied; public browsing still works.");
        show.Clicked += async (_, _) => await Run(async () => status.Text = await local.ShowPublicSampleAsync(PublicId) ? "Post/schedule request accepted. Inspect actual presentation and tap behavior on device." : "Notifications unavailable, disabled or not granted. No prompt was forced.");
        route.Clicked += (_, _) => { router.Receive(input.Text, directLink: true); status.Text = router.Status; };
        retry.Clicked += (_, _) => { router.Retry(); status.Text = router.Status; };
        clear.Clicked += (_, _) => { router.Clear(); status.Text = router.Status; };
        share.Clicked += async (_, _) => await Run(async () =>
        {
            await Share.Default.RequestAsync(new ShareTextRequest { Title = "Share public catalog lab link", Text = LinkTarget.PublicShare(PublicId) });
            status.Text = "Share sheet returned; no recipient or delivery is confirmed.";
        });
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 24, Spacing = 12, Children =
        {
            new Label { Text = "Notifications, links and sharing", FontSize = 25 },
            new Label { Text = "Custom-scheme teaching links need this app installed and have no browser fallback or verified domain association. Only public catalog links are shared. Local notification arrival does not navigate; a tap queues a validated hint. Private request reads still require API authorization. Push transport/device SDK is unconfigured; no push token is acquired or submitted by this page." },
            permission, show, input, route, share, retry, clear, status
        }}};
        async Task Run(Func<Task> action) { try { await action(); } catch { status.Text = "The native operation was not confirmed. Inspect the lab worksheet; no raw payload or token is shown."; } }
    }
}
