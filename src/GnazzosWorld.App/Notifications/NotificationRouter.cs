using GnazzosWorld.Core.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace GnazzosWorld.App.Notifications;

public sealed class NotificationRouter(RouteInbox inbox, TimeProvider clock)
{
    private Window? window;
    private bool foreground, busy;
    public string Status { get; private set; } = "No inbound route has been handled.";
    public void Receive(string? payload, bool directLink = false)
    {
        try
        {
            var now = clock.GetUtcNow();
            var item = directLink ? LinkTarget.Parse(payload) is { } link ? NotificationEnvelope.Local(link, now) : null
                : NotificationEnvelope.Decode(payload, now);
            Status = item is not null && inbox.Accept(item, now) ? "Validated routing hint queued." : "Routing hint rejected, duplicate, expired or inbox full.";
            if (foreground) Retry();
        }
        catch { Status = "The routing hint could not be stored; no navigation was confirmed."; }
    }
    public void Ready(Window value) { window = value; foreground = true; Retry(); }
    public void Background() => foreground = false;
    public void Clear() { inbox.Clear(); Status = "Pending hints and recent duplicate receipts cleared locally."; }
    public void Retry() => window?.Dispatcher.Dispatch(async () => await PumpAsync());
    private async Task PumpAsync()
    {
        if (busy || !foreground || window?.Page is not Shell shell) return;
        busy = true;
        try
        {
            while (foreground && inbox.Peek(clock.GetUtcNow()) is { } next)
            {
                var route = next.Target.Kind == LinkKind.Catalog ? "details" : "linked-request";
                await shell.GoToAsync(route, new ShellNavigationQueryParameters { ["id"] = next.Target.Id });
                inbox.Complete(next.MessageId); Status = "Navigation completed; server authorization is still required for private reads.";
            }
        }
        catch { Status = "Navigation did not complete. Pending hint retained; retry explicitly from the lab."; }
        finally { busy = false; }
    }
}
public static class InboundNotificationBridge
{
    public const string PayloadKey = "gnazzo.notification.payload";
    public static void Receive(string? text, bool directLink = false)
    {
        // Native callbacks can arrive before a visible page. Persist first; navigation waits for Ready.
        try { MainThread.BeginInvokeOnMainThread(() => IPlatformApplication.Current?.Services.GetRequiredService<NotificationRouter>().Receive(text, directLink)); }
        catch { /* Never expose raw payload/provider exceptions from an OS callback. */ }
    }
}
