using Auth0.OidcClient;
using GnazzosWorld.Core.Identity;

namespace GnazzosWorld.App.Identity;

public sealed class Auth0SessionProvider : ISessionProvider
{
    private Auth0Client? client;
    private Auth0Client Client
    {
        get
        {
            var options = AuthenticationConfiguration.ApiSession ?? throw new SessionUnconfiguredException();
            return client ??= new Auth0Client(new Auth0ClientOptions
            {
                Domain = options.Identity.Domain, ClientId = options.Identity.ClientId,
                RedirectUri = NativeIdentitySettings.Callback,
                Scope = "openid profile offline_access requests:read" + (AuthenticationConfiguration.EnableOrderLabScopes ? " orders:read orders:write" : ""), LoadProfile = false,
                Browser = new ProviderBrowser(options.Identity), EnableTelemetry = false
            });
        }
    }
    public async Task<SessionGrant> SignInAsync(CancellationToken ct)
    {
        var options = AuthenticationConfiguration.ApiSession ?? throw new SessionUnconfiguredException();
        var result = await Client.LoginAsync(new { audience = options.Audience }, ct);
        ct.ThrowIfCancellationRequested();
        if (result.IsError) throw new InteractiveSignInRequiredException();
        var subject = result.User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject)) throw new InteractiveSignInRequiredException();
        return new(options.Identity.Issuer, subject, options.Audience, result.AccessToken,
            string.IsNullOrEmpty(result.RefreshToken) ? null : result.RefreshToken, result.AccessTokenExpiration);
    }
    public async Task<SessionGrant> RefreshAsync(SessionGrant previous, CancellationToken ct)
    {
        if (previous.RefreshToken is null) throw new InteractiveSignInRequiredException();
        var result = await Client.RefreshTokenAsync(previous.RefreshToken, cancellationToken: ct);
        ct.ThrowIfCancellationRequested();
        if (result.IsError) throw new InteractiveSignInRequiredException();
        // Refresh is bound to the previous credential. A missing replacement is
        // permitted by nonrotating configurations; a returned replacement wins.
        return new(previous.Issuer, previous.Subject, previous.Audience, result.AccessToken,
            string.IsNullOrEmpty(result.RefreshToken) ? previous.RefreshToken : result.RefreshToken,
            result.AccessTokenExpiration);
    }
}
