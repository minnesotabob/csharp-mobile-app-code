using System.Net;
using System.Net.Http.Headers;
using System.Text;
using GnazzosWorld.Core.Networking;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class HttpCatalogTests
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;
    private static readonly Uri Endpoint = new("https://catalog.example.invalid/catalog/items");
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Calls++; return send(request, ct); }
    }
    private static HttpResponseMessage Json(string json = "[]") => new(HttpStatusCode.OK)
    { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private sealed class Rig : IDisposable
    {
        public readonly Handler Handler;
        public readonly HttpClient Client;
        public readonly List<TimeSpan> Waits = new();
        public readonly HttpCatalogSource Source;
        public Rig(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
            TimeProvider? clock = null, Func<TimeSpan, CancellationToken, Task>? delay = null)
        {
            Handler = new(send); Client = new(Handler) { Timeout = Timeout.InfiniteTimeSpan };
            Source = new(Client, Endpoint, clock ?? TimeProvider.System,
                delay ?? ((wait, ct) => { ct.ThrowIfCancellationRequested(); Waits.Add(wait); return Task.CompletedTask; }), () => 0);
        }
        public void Dispose() => Client.Dispose();
    }
    private static Task<CatalogFetchException> Failed(Rig rig)
        => Assert.ThrowsAsync<CatalogFetchException>(() => rig.Source.LoadAsync(TestToken));

    [Fact]
    public async Task Get_uses_fixed_endpoint_and_json_accept_header()
    {
        using var rig = new Rig((request, ct) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method); Assert.Equal(Endpoint, request.RequestUri);
            Assert.Contains(request.Headers.Accept, h => h.MediaType == "application/json");
            Assert.Null(request.Content); Assert.Null(request.Headers.Authorization);
            return Task.FromResult(Json());
        });
        Assert.Empty(await rig.Source.LoadAsync(TestToken)); Assert.Equal(1, rig.Handler.Calls);
    }
    [Fact]
    public async Task Demo_transport_returns_bundled_validated_records()
    {
        using var source = new DemoCatalogSource();
        var items = await source.LoadAsync(TestToken);
        Assert.Equal(5, items.Count); Assert.Contains(items, i => i.Kind == GnazzosWorld.Core.Catalog.ContentKind.Book);
    }
    [Theory]
    [InlineData("{")] [InlineData("null")] [InlineData("{}")] [InlineData("[{}]")]
    public async Task Malformed_or_invalid_catalog_is_not_retried(string json)
    {
        using var rig = new Rig((r, ct) => Task.FromResult(Json(json)));
        Assert.Equal(CatalogFetchProblem.InvalidPayload, (await Failed(rig)).Problem);
        Assert.Equal(1, rig.Handler.Calls); Assert.Empty(rig.Waits);
    }
    [Fact]
    public async Task Html_success_is_rejected_without_exposing_body()
    {
        using var rig = new Rig((r, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("secret gateway login") }));
        var error = await Failed(rig);
        Assert.Equal(CatalogFetchProblem.InvalidPayload, error.Problem);
        Assert.DoesNotContain("secret", error.ToString());
    }
    [Theory]
    [InlineData(400)] [InlineData(401)] [InlineData(403)] [InlineData(404)] [InlineData(204)] [InlineData(302)] [InlineData(501)]
    public async Task Permanent_or_unexpected_status_is_not_retried(int status)
    {
        using var rig = new Rig((r, ct) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)));
        Assert.Equal((HttpStatusCode)status, (await Failed(rig)).Status);
        Assert.Equal(1, rig.Handler.Calls);
    }
    [Theory]
    [InlineData(408)] [InlineData(429)] [InlineData(500)] [InlineData(502)] [InlineData(503)] [InlineData(504)]
    public async Task Transient_status_retries_are_bounded(int status)
    {
        using var rig = new Rig((r, ct) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)));
        Assert.Equal(CatalogFetchProblem.HttpStatus, (await Failed(rig)).Problem);
        Assert.Equal(3, rig.Handler.Calls);
        Assert.Equal(new[] { TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(400) }, rig.Waits);
    }
    [Fact]
    public async Task Retry_after_delta_is_honored_and_response_disposed_before_wait()
    {
        var content = new TrackedContent(); var calls = 0;
        using var rig = new Rig((r, ct) =>
        {
            if (++calls != 1) return Task.FromResult(Json());
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = content };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(2));
            return Task.FromResult(response);
        }, delay: (wait, ct) =>
        { Assert.True(content.Disposed); Assert.Equal(TimeSpan.FromSeconds(2), wait); return Task.CompletedTask; });
        Assert.Empty(await rig.Source.LoadAsync(TestToken)); Assert.Equal(2, calls);
    }
    [Fact]
    public async Task Long_retry_after_is_returned_without_retrying_early()
    {
        using var rig = new Rig((r, ct) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(60));
            return Task.FromResult(response);
        });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Failed(rig)).Status);
        Assert.Equal(1, rig.Handler.Calls); Assert.Empty(rig.Waits);
    }
    [Theory]
    [InlineData(3, 3)] [InlineData(-3, 0)]
    public async Task Retry_after_date_uses_service_clock_and_clamps_past_date(int offset, int expected)
    {
        var clock = new Clock(); var calls = 0;
        using var rig = new Rig((r, ct) =>
        {
            if (++calls > 1) return Task.FromResult(Json());
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(clock.GetUtcNow().AddSeconds(offset));
            return Task.FromResult(response);
        }, clock);
        await rig.Source.LoadAsync(TestToken); Assert.Equal(TimeSpan.FromSeconds(expected), Assert.Single(rig.Waits));
    }
    [Theory]
    [InlineData(HttpRequestError.ConnectionError)] [InlineData(HttpRequestError.NameResolutionError)]
    public async Task Transient_transport_failures_retry(HttpRequestError error)
    {
        var calls = 0;
        using var rig = new Rig((r, ct) =>
        {
            if (++calls < 3) throw new HttpRequestException(error);
            return Task.FromResult(Json());
        });
        Assert.Empty(await rig.Source.LoadAsync(TestToken)); Assert.Equal(3, calls);
    }
    [Fact]
    public async Task Certificate_failure_is_not_retried_or_leaked()
    {
        using var rig = new Rig((r, ct) => throw new HttpRequestException(HttpRequestError.SecureConnectionError, "secret host details"));
        var error = await Failed(rig); Assert.Equal(CatalogFetchProblem.Network, error.Problem);
        Assert.Equal(1, rig.Handler.Calls); Assert.DoesNotContain("secret", error.ToString());
    }
    [Fact]
    public async Task Oversize_content_length_is_rejected_before_parsing()
    {
        using var rig = new Rig((r, ct) =>
        {
            var response = Json(); response.Content.Headers.ContentLength = HttpCatalogSource.MaximumBytes + 1L;
            return Task.FromResult(response);
        });
        Assert.Equal(CatalogFetchProblem.TooLarge, (await Failed(rig)).Problem);
    }
    [Fact]
    public async Task Unknown_length_stream_is_bounded_and_disposed()
    {
        var stream = new NonSeekStream(new byte[HttpCatalogSource.MaximumBytes + 1]);
        using var rig = new Rig((r, ct) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return Task.FromResult(response);
        });
        Assert.Equal(CatalogFetchProblem.TooLarge, (await Failed(rig)).Problem);
        Assert.True(stream.Disposed); Assert.Equal(1, rig.Handler.Calls);
    }
    [Fact]
    public async Task Caller_cancellation_before_send_performs_no_request()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestToken); cts.Cancel();
        using var rig = new Rig((r, ct) => Task.FromResult(Json()));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rig.Source.LoadAsync(cts.Token));
        Assert.Equal(0, rig.Handler.Calls);
    }
    [Fact]
    public async Task Cancellation_during_backoff_does_not_send_another_request()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestToken);
        using var rig = new Rig((r, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
            delay: (wait, ct) => { cts.Cancel(); ct.ThrowIfCancellationRequested(); return Task.CompletedTask; });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rig.Source.LoadAsync(cts.Token));
        Assert.Equal(1, rig.Handler.Calls);
    }
    [Fact]
    public async Task Overall_deadline_includes_body_reads()
    {
        var clock = new Clock(); var stream = new DeadlineStream(clock);
        using var rig = new Rig((r, ct) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return Task.FromResult(response);
        }, clock);
        Assert.Equal(CatalogFetchProblem.Deadline, (await Failed(rig)).Problem);
        Assert.Equal(1, rig.Handler.Calls); Assert.True(stream.Disposed);
    }
    [Theory]
    [InlineData("http://example.invalid/items")] [InlineData("/items")]
    [InlineData("https://user:password@example.invalid/items")] [InlineData("https://example.invalid/items#fragment")]
    public void Invalid_endpoint_configuration_is_rejected(string address)
    {
        using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        Assert.Throws<ArgumentException>(() => new HttpCatalogSource(client, new Uri(address, UriKind.RelativeOrAbsolute), TimeProvider.System));
    }
    [Fact]
    public void A_second_client_timeout_is_rejected()
    {
        using var client = new HttpClient();
        Assert.Throws<ArgumentException>(() => new HttpCatalogSource(client, Endpoint, TimeProvider.System));
    }
    private sealed class TrackedContent : StringContent
    {
        public bool Disposed;
        public TrackedContent() : base("ignored") { }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
    private class NonSeekStream(byte[] bytes) : MemoryStream(bytes)
    {
        public bool Disposed;
        public override bool CanSeek => false;
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
    private sealed class DeadlineStream(Clock clock) : NonSeekStream([])
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { clock.Fire(); cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult(0); }
    }
    private sealed class Clock : TimeProvider
    {
        private TimerCallback? callback; private object? state;
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
        public void Fire() => callback!(state);
        public override ITimer CreateTimer(TimerCallback cb, object? value, TimeSpan dueTime, TimeSpan period)
        { callback = cb; state = value; return new Timer(); }
        private sealed class Timer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
