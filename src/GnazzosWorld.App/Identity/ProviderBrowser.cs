using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient.Browser;
using GnazzosWorld.Core.Identity;
using Microsoft.Maui.Authentication;

namespace GnazzosWorld.App.Identity;

// Own the platform browser boundary; the provider SDK owns PKCE/state/nonce/token validation.
public sealed class ProviderBrowser(NativeIdentitySettings settings) : Duende.IdentityModel.OidcClient.Browser.IBrowser
{
    public bool WasCanceled { get; private set; }
    public void BeginAttempt() => WasCanceled = false;
    public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default)
    {
        settings.ValidateBrowserRequest(options.StartUrl, options.EndUrl);
        try
        {
            var result = await WebAuthenticator.Default.AuthenticateAsync(
                new Uri(options.StartUrl), new Uri(options.EndUrl), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var response = new RequestUrl(options.EndUrl).Create(new Parameters(result.Properties));
            return new BrowserResult { ResultType = BrowserResultType.Success, Response = response };
        }
        catch (OperationCanceledException)
        {
            WasCanceled = true;
            return new BrowserResult { ResultType = BrowserResultType.UserCancel };
        }
    }
}
