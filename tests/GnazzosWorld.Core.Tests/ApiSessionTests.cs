using System.Net;
using System.Text;
using GnazzosWorld.Core.Identity;
using GnazzosWorld.Core.Networking;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class ApiSessionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly ApiSessionSettings Settings = new(new("fixture.auth0.com", "public-client-id"),
        "https://gnazzosworld.example/api", new Uri("https://api.gnazzosworld.example/"));
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private static SessionGrant Grant(Clock clock, string access = "access-one", string refresh = "refresh-one", string subject = "reader-a")
        => new(Settings.Identity.Issuer, subject, Settings.Audience, access, refresh, clock.Now.AddMinutes(5));
    private sealed class Vault : ISessionVault
    {
        internal string? Text;
        internal bool FailWrite;
        internal bool FailRead;
        public Task<SessionGrant?> ReadAsync(CancellationToken ct) => FailRead ? throw new IOException("fixture") : Task.FromResult(SessionEnvelopeCodec.Decode(Text));
        public Task WriteAsync(SessionGrant? grant, CancellationToken ct)
        { if (FailWrite) throw new IOException("fixture"); Text = SessionEnvelopeCodec.Encode(grant); return Task.CompletedTask; }
    }
    private sealed class Provider(Clock clock) : ISessionProvider
    {
        internal int RefreshCalls;
        internal Func<SessionGrant, CancellationToken, Task<SessionGrant>>? Refresh;
        public Task<SessionGrant> SignInAsync(CancellationToken ct) => Task.FromResult(Grant(clock));
        public Task<SessionGrant> RefreshAsync(SessionGrant prior, CancellationToken ct)
        { Interlocked.Increment(ref RefreshCalls); return Refresh?.Invoke(prior, ct) ?? Task.FromResult(Grant(clock, "access-two", "refresh-two")); }
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);
    }
    private static HttpResponseMessage Preview(HttpStatusCode status = HttpStatusCode.OK) => new(status)
    { Content = new StringContent("{\"id\":\"anniversary-demo\",\"summary\":\"Fictitious request\"}", Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Credential_envelope_restores_for_same_issuer_and_audience_without_parsing_a_jwt()
    {
        var clock = new Clock(); var vault = new Vault(); var provider = new Provider(clock);
        var first = new ApiSession(Settings, vault, provider, clock); await first.SignInAsync(Ct);
        var reopened = new ApiSession(Settings, vault, provider, clock);
        Assert.Equal("access-one", (await reopened.AcquireAsync(Ct)).Token); Assert.Equal(0, provider.RefreshCalls);
        Assert.DoesNotContain("access-one", Grant(clock).ToString());
        Assert.DoesNotContain("access-one", (await reopened.AcquireAsync(Ct)).ToString());
    }
    [Fact]
    public async Task Concurrent_expiring_requests_rotate_once_and_publish_after_storage()
    {
        var clock = new Clock(); var vault = new Vault(); var provider = new Provider(clock);
        var session = new ApiSession(Settings, vault, provider, clock); await session.SignInAsync(Ct); clock.Now = clock.Now.AddMinutes(5);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.Refresh = async (_, ct) => { entered.SetResult(); await release.Task.WaitAsync(ct); return Grant(clock, "new-access", "new-refresh"); };
        var one = session.AcquireAsync(Ct); await entered.Task.WaitAsync(Ct); var two = session.AcquireAsync(Ct);
        release.SetResult(); var leases = await Task.WhenAll(one, two);
        Assert.Equal(1, provider.RefreshCalls); Assert.All(leases, l => Assert.Equal("new-access", l.Token));
        Assert.Equal("new-refresh", SessionEnvelopeCodec.Decode(vault.Text)!.RefreshToken);
    }
    [Fact]
    public async Task Rotation_write_failure_blocks_use_until_cleanup_is_successful()
    {
        var clock = new Clock(); var vault = new Vault(); var provider = new Provider(clock);
        var session = new ApiSession(Settings, vault, provider, clock); await session.SignInAsync(Ct);
        clock.Now = clock.Now.AddMinutes(5); vault.FailWrite = true;
        await Assert.ThrowsAsync<SessionStorageException>(() => session.AcquireAsync(Ct));
        await Assert.ThrowsAsync<SessionStorageException>(() => session.AcquireAsync(Ct));
        Assert.Equal(1, provider.RefreshCalls);
        vault.FailWrite = false; await session.SignOutLocalAsync(Ct);
        Assert.Equal(SessionEnvelopeCodec.SignedOut, vault.Text);
        await Assert.ThrowsAsync<InteractiveSignInRequiredException>(() => session.AcquireAsync(Ct));
    }
    [Fact]
    public async Task Uncertain_refresh_is_not_replayed_after_reopen()
    {
        var clock = new Clock(); var vault = new Vault(); var provider = new Provider(clock);
        var session = new ApiSession(Settings, vault, provider, clock); await session.SignInAsync(Ct); clock.Now = clock.Now.AddMinutes(5);
        provider.Refresh = (_, _) => throw new IOException("Reply lost after potential rotation.");
        await Assert.ThrowsAsync<InteractiveSignInRequiredException>(() => session.AcquireAsync(Ct));
        var reopened = new ApiSession(Settings, vault, provider, clock);
        await Assert.ThrowsAsync<InteractiveSignInRequiredException>(() => reopened.AcquireAsync(Ct));
        Assert.Equal(1, provider.RefreshCalls);
    }
    [Fact]
    public async Task Sign_out_fences_a_late_refresh_before_it_can_repopulate_storage()
    {
        var clock = new Clock(); var vault = new Vault(); var provider = new Provider(clock);
        var session = new ApiSession(Settings, vault, provider, clock); await session.SignInAsync(Ct); clock.Now = clock.Now.AddMinutes(5);
        var finish = new TaskCompletionSource<SessionGrant>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.Refresh = (_, _) => finish.Task;
        var acquiring = session.AcquireAsync(Ct); var ending = session.SignOutLocalAsync(Ct);
        finish.SetResult(Grant(clock, "late-access", "late-refresh"));
        await Assert.ThrowsAsync<SessionEndedException>(() => acquiring); await ending;
        Assert.Equal(SessionEnvelopeCodec.SignedOut, vault.Text);
    }
    [Fact]
    public async Task Cleanup_failure_is_reported_and_current_process_remains_blocked()
    {
        var clock = new Clock(); var vault = new Vault(); var provider = new Provider(clock);
        var session = new ApiSession(Settings, vault, provider, clock); await session.SignInAsync(Ct); var lease = await session.AcquireAsync(Ct);
        vault.FailWrite = true;
        await Assert.ThrowsAsync<SessionStorageException>(() => session.SignOutLocalAsync(Ct)); Assert.False(session.IsCurrent(lease));
        await Assert.ThrowsAsync<SessionStorageException>(() => session.AcquireAsync(Ct));
        // The old stored value may still exist: do not promise persistence was cleared.
        Assert.NotEqual(SessionEnvelopeCodec.SignedOut, vault.Text);
    }
    [Fact]
    public async Task Corrupt_or_unreadable_storage_has_no_plaintext_fallback()
    {
        var clock = new Clock(); var vault = new Vault { Text = "corrupt-envelope" }; var provider = new Provider(clock);
        var session = new ApiSession(Settings, vault, provider, clock);
        await Assert.ThrowsAsync<SessionStorageException>(() => session.AcquireAsync(Ct));
        vault.Text = null; await Assert.ThrowsAsync<SessionStorageException>(() => session.AcquireAsync(Ct));
        await session.SignOutLocalAsync(Ct); vault.FailRead = true;
        var reopened = new ApiSession(Settings, vault, provider, clock);
        await Assert.ThrowsAsync<SessionStorageException>(() => reopened.AcquireAsync(Ct));
    }
    [Fact]
    public async Task A_different_account_on_refresh_requires_interactive_recovery()
    {
        var clock = new Clock(); var vault = new Vault(); var provider = new Provider(clock);
        var session = new ApiSession(Settings, vault, provider, clock); await session.SignInAsync(Ct); clock.Now = clock.Now.AddMinutes(5);
        provider.Refresh = (_, _) => Task.FromResult(Grant(clock, subject: "reader-b"));
        await Assert.ThrowsAsync<InteractiveSignInRequiredException>(() => session.AcquireAsync(Ct));
        Assert.Equal(SessionEnvelopeCodec.SignedOut, vault.Text);
    }
    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Found)]
    public async Task Forbidden_missing_or_redirect_response_does_not_refresh_or_follow(HttpStatusCode status)
    {
        var clock = new Clock(); var vault = new Vault(); var provider = new Provider(clock);
        var session = new ApiSession(Settings, vault, provider, clock); await session.SignInAsync(Ct); var calls = 0;
        using var http = new HttpClient(new Handler((request, _) =>
        {
            calls++; Assert.Equal("api.gnazzosworld.example", request.RequestUri!.Host);
            Assert.Equal("access-one", request.Headers.Authorization!.Parameter);
            return Task.FromResult(Preview(status));
        }));
        var client = new ProtectedRequestClient(http, session);
        var error = await Assert.ThrowsAsync<RequestAccessException>(() => client.GetAsync("anniversary-demo", Ct));
        Assert.Equal(status, error.Status); Assert.Equal(1, calls); Assert.Equal(0, provider.RefreshCalls);
        Assert.Null(http.DefaultRequestHeaders.Authorization);
    }
    [Fact]
    public async Task Unauthorized_response_clears_only_its_session_and_does_not_retry()
    {
        var clock = new Clock(); var vault = new Vault(); var provider = new Provider(clock);
        var session = new ApiSession(Settings, vault, provider, clock); await session.SignInAsync(Ct); var calls = 0;
        using var http = new HttpClient(new Handler((_, _) => { calls++; return Task.FromResult(Preview(HttpStatusCode.Unauthorized)); }));
        await Assert.ThrowsAsync<InteractiveSignInRequiredException>(() => new ProtectedRequestClient(http, session).GetAsync("anniversary-demo", Ct));
        Assert.Equal(1, calls); Assert.Equal(0, provider.RefreshCalls); Assert.Equal(SessionEnvelopeCodec.SignedOut, vault.Text);
    }
    [Fact]
    public async Task Old_response_cannot_publish_data_after_local_sign_out()
    {
        var clock = new Clock(); var vault = new Vault(); var provider = new Provider(clock);
        var session = new ApiSession(Settings, vault, provider, clock); await session.SignInAsync(Ct);
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new Handler(async (_, ct) => { arrived.SetResult(); await release.Task.WaitAsync(ct); return Preview(); }));
        var reading = new ProtectedRequestClient(http, session).GetAsync("anniversary-demo", Ct);
        await arrived.Task.WaitAsync(Ct); await session.SignOutLocalAsync(Ct); release.SetResult();
        await Assert.ThrowsAsync<SessionEndedException>(() => reading);
    }
    [Fact]
    public async Task Preview_body_is_bounded_and_ids_cannot_change_the_origin_or_path()
    {
        var clock = new Clock(); var vault = new Vault(); var provider = new Provider(clock);
        var session = new ApiSession(Settings, vault, provider, clock); await session.SignInAsync(Ct);
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(new string('x', 16385), Encoding.UTF8, "application/json") })));
        var client = new ProtectedRequestClient(http, session);
        await Assert.ThrowsAsync<InvalidDataException>(() => client.GetAsync("anniversary-demo", Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => client.GetAsync("../other", Ct));
    }
}
