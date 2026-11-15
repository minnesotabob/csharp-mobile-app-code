using Auth0.OidcClient;
using GnazzosWorld.Core.Identity;

namespace GnazzosWorld.App.Identity;

public sealed class Auth0InteractiveSignIn : IInteractiveSignIn
{
    private readonly NativeIdentitySettings? settings = AuthenticationConfiguration.Current;
    private Auth0Client? client;
    private ProviderBrowser? browser;
    public Uri? ExpectedIssuer => settings?.Issuer;
    public async Task<SignInReply> LoginAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (settings is null) return new(SignInOutcome.Unconfigured);
        browser ??= new ProviderBrowser(settings);
        client ??= new Auth0Client(new Auth0ClientOptions
        {
            Domain = settings.Domain, ClientId = settings.ClientId,
            RedirectUri = NativeIdentitySettings.Callback,
            Scope = "openid profile", LoadProfile = false,
            Browser = browser, EnableTelemetry = false
        });
        browser.BeginAttempt();
        var result = await client.LoginAsync(cancellationToken: ct);
        ct.ThrowIfCancellationRequested();
        if (result.IsError) return new(browser.WasCanceled ? SignInOutcome.Canceled : SignInOutcome.Rejected);
        // LoginAsync's successful result has passed the SDK's OIDC checks. Do not decode JWTs here.
        var subject = result.User.FindFirst("sub")?.Value;
        var name = result.User.FindFirst("name")?.Value;
        if (string.IsNullOrWhiteSpace(subject)) return new(SignInOutcome.Rejected);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120 || name.Any(char.IsControl)) name = "Signed-in reader";
        return new(SignInOutcome.Succeeded, new(settings.Issuer, subject, name));
        // Tokens in result are intentionally neither exposed, logged nor persisted.
    }
}
