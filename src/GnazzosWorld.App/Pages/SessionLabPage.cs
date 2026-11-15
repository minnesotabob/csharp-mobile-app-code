using GnazzosWorld.Core.Identity;
using GnazzosWorld.Core.Networking;

namespace GnazzosWorld.App.Pages;

public sealed class SessionLabPage : ContentPage
{
    private readonly ApiSession session;
    private readonly ProtectedRequestClient previews;
    private readonly Label status = new() { Text = "API sessions are optional. Use the Chapter 23 setup before this lab." };
    private readonly Entry requestId = new() { Placeholder = "Your test request ID", Text = "anniversary-demo" };
    private bool running;
    public SessionLabPage(ApiSession session, ProtectedRequestClient previews)
    {
        this.session = session; this.previews = previews; Title = "Session lab";
        var signIn = new Button { Text = "Sign in for the API lab" };
        signIn.Clicked += async (_, _) => await Run(async () =>
        { await session.SignInAsync(); status.Text = "API credentials saved locally. No request has been authorized yet."; });
        var read = new Button { Text = "Read my test request" };
        read.Clicked += async (_, _) => await Run(async () =>
        { var result = await previews.GetAsync(requestId.Text ?? ""); status.Text = result.Summary; });
        var signOut = new Button { Text = "Sign out locally" };
        // Sign-out must be allowed during a pending read/refresh so it can fence its result.
        signOut.Clicked += async (_, _) =>
        {
            try { await session.SignOutLocalAsync(); status.Text = "Local API credentials cleared. The provider browser session may remain active."; }
            catch { status.Text = "Local use is blocked, but stored credentials could not be cleared. Retry sign-out."; }
        };
        Content = new ScrollView { Content = new VerticalStackLayout
        { Padding = 24, Spacing = 16, Children = { new Label { Text = "API session teaching lab", FontSize = 26 },
            new Label { Text = "This separate lab does not migrate existing favorites or drafts. The server starts with no customer requests." },
            status, signIn, requestId, read, signOut } } };
    }
    private async Task Run(Func<Task> action)
    {
        if (running) return; running = true;
        try { await action(); }
        catch (SessionUnconfiguredException ex) { status.Text = ex.Message; }
        catch (SessionStorageException ex) { status.Text = ex.Message; }
        catch (SessionEndedException ex) { status.Text = ex.Message; }
        catch (InteractiveSignInRequiredException ex) { status.Text = ex.Message; }
        catch { status.Text = "The lab operation could not complete. Public browsing remains available."; }
        finally { running = false; }
    }
}
