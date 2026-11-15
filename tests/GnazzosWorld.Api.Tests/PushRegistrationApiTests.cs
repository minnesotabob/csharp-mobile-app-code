using System.Net;
using System.Net.Http.Json;
using System.Text;
using GnazzosWorld.Api;
using GnazzosWorld.Core.Notifications;
using GnazzosWorld.Core.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GnazzosWorld.Api.Tests;

public sealed class PushRegistrationApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static PushBinding Binding(string? id = null, long generation = 1) => new(id ?? Guid.NewGuid().ToString("N"), "fcm", "synthetic-token-" + Guid.NewGuid().ToString("N"), generation);
    private static string PathFor(PushBinding b) => "/api/v1/notification-installations/" + b.InstallationId;
    [Fact]
    public async Task Registration_requires_credential_and_dedicated_permission()
    {
        using var f = new RequestAuthorizationTests.Factory(); var b = Binding();
        using var anonymous = f.Client(); using var a = await anonymous.PutAsJsonAsync(PathFor(b), b, Ct); Assert.Equal(HttpStatusCode.Unauthorized, a.StatusCode);
        using var readOnly = f.Client(f.Token()); using var r = await readOnly.PutAsJsonAsync(PathFor(b), b, Ct); Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }
    [Fact]
    public async Task Valid_registration_returns_no_token_body_and_no_store()
    {
        using var f = new RequestAuthorizationTests.Factory(); using var c = f.Client(f.Token(scope: "notifications:manage")); var b = Binding();
        using var r = await c.PutAsJsonAsync(PathFor(b), b, Ct); Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
        Assert.Empty(await r.Content.ReadAsByteArrayAsync(Ct)); Assert.Contains("no-store", r.Headers.CacheControl!.ToString());
        Assert.Equal(1, f.Services.GetRequiredService<PushInstallationRegistry>().CountFor("https://fixture.auth0.com/\nreader-a"));
    }
    [Fact]
    public async Task Foreign_owner_cannot_replace_or_remove_current_installation()
    {
        using var f = new RequestAuthorizationTests.Factory(); using var a = f.Client(f.Token(scope: "notifications:manage"));
        using var b = f.Client(f.Token(subject: "reader-b", scope: "notifications:manage")); var binding = Binding();
        using var first = await a.PutAsJsonAsync(PathFor(binding), binding, Ct); Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        using var replace = await b.PutAsJsonAsync(PathFor(binding), binding with { Generation = 2 }, Ct); Assert.Equal(HttpStatusCode.Conflict, replace.StatusCode);
        using var remove = await b.DeleteAsync(PathFor(binding), Ct); Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        Assert.Equal(1, f.Services.GetRequiredService<PushInstallationRegistry>().CountFor("https://fixture.auth0.com/\nreader-a"));
    }
    [Fact]
    public async Task Stale_generation_is_rejected_and_new_rotation_replaces_same_installation()
    {
        using var f = new RequestAuthorizationTests.Factory(); using var c = f.Client(f.Token(scope: "notifications:manage")); var b = Binding();
        using var first = await c.PutAsJsonAsync(PathFor(b), b, Ct); Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        using var stale = await c.PutAsJsonAsync(PathFor(b), b, Ct); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var next = await c.PutAsJsonAsync(PathFor(b), b with { Generation = 2, Token = "synthetic-rotated" }, Ct); Assert.Equal(HttpStatusCode.NoContent, next.StatusCode);
        using var deleted = await c.DeleteAsync(PathFor(b), Ct); Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(0, f.Services.GetRequiredService<PushInstallationRegistry>().CountFor("https://fixture.auth0.com/\nreader-a"));
    }
    [Fact]
    public async Task Client_cannot_supply_owner_field_or_oversized_body()
    {
        using var f = new RequestAuthorizationTests.Factory(); using var c = f.Client(f.Token(scope: "notifications:manage")); var b = Binding();
        using var body = new StringContent("{\"owner\":\"reader-b\"}", Encoding.UTF8, "application/json");
        using var r = await c.PutAsync(PathFor(b), body, Ct); Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        using var huge = new StringContent(new string('x', 4097), Encoding.UTF8, "application/json");
        using var rejected = await c.PutAsync(PathFor(b), huge, Ct); Assert.Equal((HttpStatusCode)413, rejected.StatusCode);
    }
    [Fact]
    public async Task Per_owner_installation_cap_does_not_accept_a_fifth_device()
    {
        using var f = new RequestAuthorizationTests.Factory(); using var c = f.Client(f.Token(scope: "notifications:manage"));
        for (var i = 0; i < 4; i++) { var b = Binding(); using var r = await c.PutAsJsonAsync(PathFor(b), b, Ct); Assert.Equal(HttpStatusCode.NoContent, r.StatusCode); }
        var fifth = Binding(); using var rejected = await c.PutAsJsonAsync(PathFor(fifth), fifth, Ct); Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
    }
    [Fact]
    public async Task Actual_session_sink_can_register_and_remove_through_authorized_server_pipeline()
    {
        using var f = new RequestAuthorizationTests.Factory(); using var bootstrap = f.Client();
        var grant = new SessionGrant(new("https://fixture.auth0.com/"), "reader-a", "https://gnazzosworld.example/api",
            f.Token(scope: "notifications:manage"), null, DateTimeOffset.UtcNow.AddMinutes(5));
        var settings = new ApiSessionSettings(new("fixture.auth0.com", "public-client-id"), grant.Audience, new("https://localhost"));
        var session = new ApiSession(settings, new Vault(grant), new Provider(), TimeProvider.System);
        using var sink = new HttpPushRegistrationSink(session, f.Server.CreateHandler()); var b = Binding();
        await sink.UpsertAsync(b, Ct); await sink.RemoveAsync(b.InstallationId, Ct);
        Assert.Equal(0, f.Services.GetRequiredService<PushInstallationRegistry>().CountFor("https://fixture.auth0.com/\nreader-a"));
    }
    [Fact]
    public void Volatile_registration_expires_and_does_not_become_identity()
    {
        var clock = new Clock(); var registry = new PushInstallationRegistry(clock); var b = Binding();
        Assert.True(registry.Upsert("owner-a", b)); clock.Now = clock.Now.AddHours(25); Assert.Equal(0, registry.CountFor("owner-a"));
        Assert.True(registry.Upsert("owner-b", b));
    }
    [Fact]
    public void Token_cannot_be_attached_to_a_second_installation_while_bound()
    {
        var registry = new PushInstallationRegistry(new Clock()); var b = Binding(); Assert.True(registry.Upsert("owner-a", b));
        Assert.False(registry.Upsert("owner-b", b with { InstallationId = Guid.NewGuid().ToString("N") }));
    }
    private sealed class Clock : TimeProvider
    { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Vault(SessionGrant grant) : ISessionVault
    {
        public Task<SessionGrant?> ReadAsync(CancellationToken ct) => Task.FromResult<SessionGrant?>(grant);
        public Task WriteAsync(SessionGrant? value, CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class Provider : ISessionProvider
    {
        public Task<SessionGrant> SignInAsync(CancellationToken ct) => throw new InvalidOperationException();
        public Task<SessionGrant> RefreshAsync(SessionGrant old, CancellationToken ct) => throw new InvalidOperationException();
    }
}
