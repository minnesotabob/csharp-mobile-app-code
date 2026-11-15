using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using GnazzosWorld.Api.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace GnazzosWorld.Api.Tests;

public sealed class RequestAuthorizationTests
{
    private const string Issuer = "https://fixture.auth0.com/";
    private const string Audience = "https://gnazzosworld.example/api";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed class Factory : WebApplicationFactory<Program>
    {
        internal readonly RSA Rsa = RSA.Create(2048);
        internal RsaSecurityKey Key => new(Rsa) { KeyId = "ephemeral-test-key" };
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/GnazzosWorld.Api")));
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    // Only the tests provide static metadata and this ephemeral signing key.
                    // Production keeps HTTPS discovery and never registers a signing private key.
                    var metadata = new OpenIdConnectConfiguration { Issuer = Issuer };
                    metadata.SigningKeys.Add(Key);
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
                    options.TokenValidationParameters.ValidIssuer = Issuer;
                    options.TokenValidationParameters.ValidAudience = Audience;
                });
                services.RemoveAll<OwnedRequestStore>();
                services.AddSingleton(new OwnedRequestStore([
                    new(Issuer, "reader-a", "anniversary-demo", "Fictitious anniversary request"),
                    new(Issuer, "reader-b", "birthday-demo", "Fictitious birthday request")
                ]));
            });
        }
        public string Token(string subject = "reader-a", string scope = "requests:read", string issuer = Issuer,
            string audience = Audience, DateTime? expires = null, RSA? signer = null, bool includeSubject = true)
        {
            var claims = new List<Claim> { new("scope", scope) };
            if (includeSubject) claims.Add(new("sub", subject));
            var expiry = expires ?? DateTime.UtcNow.AddMinutes(5);
            var token = new JwtSecurityToken(issuer, audience, claims, expiry.AddMinutes(-10), expiry,
                new SigningCredentials(signer is null ? Key : new RsaSecurityKey(signer) { KeyId = Key.KeyId }, SecurityAlgorithms.RsaSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        public HttpClient Client(string? token = null)
        {
            var c = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            c.Timeout = Timeout.InfiniteTimeSpan;
            if (token is not null) c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return c;
        }
        protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) Rsa.Dispose(); }
    }
    [Fact]
    public async Task No_credential_gets_a_challenge_while_public_catalog_stays_available()
    {
        using var f = new Factory(); using var c = f.Client();
        using var denied = await c.GetAsync("/api/v1/requests/anniversary-demo", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode); Assert.NotEmpty(denied.Headers.WwwAuthenticate);
        using var catalog = await c.GetAsync("/api/v1/catalog", Ct); Assert.Equal(HttpStatusCode.OK, catalog.StatusCode);
    }
    [Theory]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("malformed")]
    public async Task Real_bearer_handler_rejects_invalid_tokens(string defect)
    {
        using var f = new Factory(); using var other = RSA.Create(2048);
        var token = defect switch
        {
            "signature" => f.Token(signer: other),
            "issuer" => f.Token(issuer: "https://other.auth0.com/"),
            "audience" => f.Token(audience: "native-client-id"), // An ID-token-shaped audience cannot authorize the API.
            "expired" => f.Token(expires: DateTime.UtcNow.AddMinutes(-2)),
            _ => "fixture-invalid-token"
        };
        using var c = f.Client(token); using var response = await c.GetAsync("/api/v1/requests/anniversary-demo", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(token, await response.Content.ReadAsStringAsync(Ct));
    }
    [Theory]
    [InlineData("catalog:read", true)]
    [InlineData("requests:read", false)]
    public async Task Valid_authentication_without_required_scope_or_subject_is_forbidden(string scope, bool subject)
    {
        using var f = new Factory(); using var c = f.Client(f.Token(scope: scope, includeSubject: subject));
        using var response = await c.GetAsync("/api/v1/requests/anniversary-demo", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    [Fact]
    public async Task Valid_owner_can_read_only_their_record()
    {
        using var f = new Factory(); using var c = f.Client(f.Token());
        using var own = await c.GetAsync("/api/v1/requests/anniversary-demo", Ct);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode); Assert.Contains("Fictitious anniversary", await own.Content.ReadAsStringAsync(Ct));
        using var other = await c.GetAsync("/api/v1/requests/birthday-demo", Ct);
        using var absent = await c.GetAsync("/api/v1/requests/unknown", Ct);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode); Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
        using var absentBody = JsonDocument.Parse(await absent.Content.ReadAsStringAsync(Ct));
        using var otherBody = JsonDocument.Parse(await other.Content.ReadAsStringAsync(Ct));
        Assert.Equal(absentBody.RootElement.GetProperty("title").GetString(), otherBody.RootElement.GetProperty("title").GetString());
        Assert.DoesNotContain("Fictitious birthday", await other.Content.ReadAsStringAsync(Ct));
    }
    [Fact]
    public async Task Client_supplied_account_label_does_not_change_server_ownership()
    {
        using var f = new Factory(); using var c = f.Client(f.Token(subject: "reader-b"));
        using var r = await c.GetAsync("/api/v1/requests/anniversary-demo?account=reader-a", Ct);
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.DoesNotContain("anniversary request", await r.Content.ReadAsStringAsync(Ct));
    }
    [Fact]
    public async Task Default_server_has_no_customer_fixtures_and_does_not_accept_test_credentials()
    {
        using var signer = new Factory();
        using var server = new WebApplicationFactory<Program>();
        using var c = server.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        c.Timeout = Timeout.InfiniteTimeSpan;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", signer.Token());
        using var r = await c.GetAsync("/api/v1/requests/anniversary-demo", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }
}
