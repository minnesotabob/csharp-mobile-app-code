using System.Collections.Concurrent;
using System.Net;
using GnazzosWorld.Api.Security;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GnazzosWorld.Api.Tests;

public sealed class ProtectedReadSecurityTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed class Capture : ILoggerProvider
    {
        internal ConcurrentQueue<string> Events { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Sink(this, categoryName);
        public void Dispose() { }
        private sealed class Sink(Capture capture, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (category == typeof(ReadSecurityAudit).FullName)
                    capture.Events.Enqueue($"{eventId.Id}:{formatter(state, exception)}");
            }
        }
    }
    [Theory]
    [InlineData("bad!identifier")]
    [InlineData("customer.name")]
    [InlineData("%E2%98%83")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Server_rejects_noncanonical_ids_without_echoing_them(string id)
    {
        using var factory = new RequestAuthorizationTests.Factory(); using var client = factory.Client(factory.Token());
        using var response = await client.GetAsync("/api/v1/requests/" + id, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(id, await response.Content.ReadAsStringAsync(Ct));
        Assert.True(response.Headers.CacheControl?.NoStore);
    }
    [Fact]
    public async Task Protected_success_and_rejection_both_disallow_cache_storage()
    {
        using var factory = new RequestAuthorizationTests.Factory();
        using var owner = factory.Client(factory.Token()); using var anonymous = factory.Client();
        using var success = await owner.GetAsync("/api/v1/requests/anniversary-demo", Ct);
        using var denied = await anonymous.GetAsync("/api/v1/requests/anniversary-demo", Ct);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.True(success.Headers.CacheControl?.NoStore); Assert.True(denied.Headers.CacheControl?.NoStore);
    }
    [Fact]
    public async Task Aggregate_budget_returns_429_without_queueing_and_public_routes_remain_available()
    {
        using var capture = new Capture(); using var factory = new RequestAuthorizationTests.Factory(capture);
        using var client = factory.Client(factory.Token());
        for (var i = 0; i < ProtectedReadSecurity.PermitLimit; i++)
        {
            using var accepted = await client.GetAsync("/api/v1/requests/anniversary-demo", Ct);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }
        using var limited = await client.GetAsync("/api/v1/requests/anniversary-demo", Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero); Assert.True(limited.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("Fictitious anniversary", await limited.Content.ReadAsStringAsync(Ct));
        Assert.Contains("2601:Owned request read: rate_limited", capture.Events);
        using var health = await client.GetAsync("/health", Ct);
        using var catalog = await client.GetAsync("/api/v1/catalog", Ct);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode); Assert.Equal(HttpStatusCode.OK, catalog.StatusCode);
    }
    [Fact]
    public async Task Unauthorized_and_underscoped_requests_do_not_consume_the_protected_budget()
    {
        using var factory = new RequestAuthorizationTests.Factory(); using var anonymous = factory.Client();
        using var underscoped = factory.Client(factory.Token(scope: "catalog:read"));
        for (var i = 0; i <= ProtectedReadSecurity.PermitLimit; i++)
        {
            using var challenge = await anonymous.GetAsync("/api/v1/requests/anniversary-demo", Ct);
            using var forbidden = await underscoped.GetAsync("/api/v1/requests/anniversary-demo", Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, challenge.StatusCode); Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
        using var owner = factory.Client(factory.Token());
        using var response = await owner.GetAsync("/api/v1/requests/anniversary-demo", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    [Fact]
    public async Task Shared_budget_cannot_be_reset_by_switching_signed_in_subject()
    {
        using var factory = new RequestAuthorizationTests.Factory(); using var a = factory.Client(factory.Token());
        for (var i = 0; i < ProtectedReadSecurity.PermitLimit; i++)
        { using var response = await a.GetAsync("/api/v1/requests/anniversary-demo", Ct); Assert.Equal(HttpStatusCode.OK, response.StatusCode); }
        using var b = factory.Client(factory.Token(subject: "reader-b"));
        using var limited = await b.GetAsync("/api/v1/requests/birthday-demo", Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }
    [Fact]
    public async Task Audit_projection_records_decisions_without_request_supplied_secrets()
    {
        using var capture = new Capture(); using var factory = new RequestAuthorizationTests.Factory(capture);
        var token = factory.Token(); using var client = factory.Client(token);
        using var allowed = await client.GetAsync("/api/v1/requests/anniversary-demo?story=PRIVATE_STORY&account=reader-a", Ct);
        using var absent = await client.GetAsync("/api/v1/requests/birthday-demo", Ct);
        using var invalid = await client.GetAsync("/api/v1/requests/PRIVATE_SECRET!", Ct);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode); Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(new[] { "2601:Owned request read: granted", "2601:Owned request read: unavailable",
            "2601:Owned request read: invalid_identifier" }, capture.Events.ToArray());
        var text = string.Join('\n', capture.Events);
        foreach (var secret in new[] { token, "PRIVATE_STORY", "PRIVATE_SECRET", "reader-a", "anniversary-demo", "Fictitious" })
            Assert.DoesNotContain(secret, text);
    }
}
