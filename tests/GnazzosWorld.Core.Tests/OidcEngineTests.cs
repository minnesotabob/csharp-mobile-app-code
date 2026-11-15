using System.Net;
using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.Browser;
using Duende.IdentityModel.Client;
using GnazzosWorld.Core.Identity;
using Xunit;

namespace GnazzosWorld.Core.Tests;

// Protocol-engine checks only. No real discovery, signing keys, browser, or provider account.
public sealed class OidcEngineTests
{
    private sealed class NoNetwork : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; throw new InvalidOperationException("No token request should occur for these rejected callbacks.");
        }
    }
    private sealed class CanceledBrowser : IBrowser
    {
        public Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(new BrowserResult { ResultType = BrowserResultType.UserCancel });
    }
    private static OidcClient Client(NoNetwork network, IBrowser? browser = null) => new(new OidcClientOptions
    {
        ClientId = "public-client-id", Scope = "openid profile",
        RedirectUri = NativeIdentitySettings.Callback, LoadProfile = false,
        BackchannelHandler = network, Browser = browser!,
        ProviderInformation = new ProviderInformation
        {
            IssuerName = "https://fixture.auth0.com/", AuthorizeEndpoint = "https://fixture.auth0.com/authorize",
            TokenEndpoint = "https://fixture.auth0.com/oauth/token"
        },
        // Discovery is replaced by the metadata fixture; token signature requirements stay at defaults.
        Policy = new Policy { Discovery = new DiscoveryPolicy { RequireKeySet = false } }
    });
    [Fact]
    public async Task Sdk_rejects_a_callback_from_another_attempt_before_token_exchange()
    {
        using var network = new NoNetwork(); var client = Client(network);
        var state = await client.PrepareLoginAsync(cancellationToken: TestContext.Current.CancellationToken);
        var result = await client.ProcessResponseAsync(NativeIdentitySettings.Callback + "?state=unrelated-attempt&code=fixture",
            state, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.IsError); Assert.Null(result.User); Assert.Equal(0, network.Calls);
    }
    [Fact]
    public async Task Sdk_rejects_a_callback_without_authorization_code()
    {
        using var network = new NoNetwork(); var client = Client(network);
        var state = await client.PrepareLoginAsync(cancellationToken: TestContext.Current.CancellationToken);
        var result = await client.ProcessResponseAsync(NativeIdentitySettings.Callback + "?state=" + Uri.EscapeDataString(state.State),
            state, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.IsError); Assert.Equal(0, network.Calls);
    }
    [Fact]
    public async Task Sdk_reports_browser_cancellation_without_token_exchange()
    {
        using var network = new NoNetwork();
        var result = await Client(network, new CanceledBrowser()).LoginAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.IsError); Assert.Equal(0, network.Calls);
    }
}
