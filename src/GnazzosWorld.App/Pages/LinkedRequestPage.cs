using GnazzosWorld.Core.Identity;
using GnazzosWorld.Core.Networking;
using GnazzosWorld.App.Notifications;

namespace GnazzosWorld.App.Pages;

public sealed class LinkedRequestPage : ContentPage, IQueryAttributable
{
    private string? id;
    private bool busy;
    private CancellationTokenSource? active;
    private readonly Label status = new() { Text = "This link is an untrusted request identifier. Choose sign-in or an authorized read; no private data has been fetched." };
    public LinkedRequestPage(ApiSession session, ProtectedRequestClient previews, NotificationRouter router)
    {
        Title = "Linked request";
        var signIn = new Button { Text = "Sign in, then continue this request" };
        var read = new Button { Text = "Read using current API session" };
        var signOut = new Button { Text = "Sign out locally and discard pending links" };
        signIn.Clicked += async (_, _) => await Run(async ct => { await session.SignInAsync(ct); ct.ThrowIfCancellationRequested(); await ReadAsync(ct); });
        read.Clicked += async (_, _) => await Run(ReadAsync);
        signOut.Clicked += async (_, _) =>
        {
            try { active?.Cancel(); router.Clear(); await session.SignOutLocalAsync(); status.Text = "Local API session cleared. No pending links remain."; }
            catch { status.Text = "Local cleanup could not be confirmed. Retry; no private success is reported."; }
        };
        async Task ReadAsync(CancellationToken ct)
        {
            if (!RequestIdPolicy.IsValid(id)) throw new InvalidDataException();
            var result = await previews.GetAsync(id!, ct); ct.ThrowIfCancellationRequested(); status.Text = result.Summary;
        }
        Content = new VerticalStackLayout { Padding = 24, Spacing = 14, Children = { status, signIn, read, signOut } };
    }
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    { id = query.TryGetValue("id", out var value) && value is string key && RequestIdPolicy.IsValid(key) ? key : null; }
    protected override void OnDisappearing() { active?.Cancel(); base.OnDisappearing(); }
    private async Task Run(Func<CancellationToken, Task> action)
    {
        if (busy) return; busy = true; using var cancellation = new CancellationTokenSource(); active = cancellation;
        try { await action(cancellation.Token); }
        catch (OperationCanceledException) { }
        catch { status.Text = "The request was not confirmed for the current API session. Configure the provider, sign in or retry an authorized read."; }
        finally { active = null; busy = false; }
    }
}
