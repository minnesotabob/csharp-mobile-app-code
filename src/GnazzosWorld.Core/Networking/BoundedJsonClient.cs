using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GnazzosWorld.Core.Networking;

// GET-only adapter. The caller owns the reusable client and configures redirect/TLS policy.
public sealed class BoundedJsonClient
{
    public const int MaximumBytes = 1_048_576;
    public const int MaximumAttempts = 3;
    public static readonly TimeSpan Deadline = TimeSpan.FromSeconds(20);
    private readonly HttpClient client;
    private readonly Uri endpoint;
    private readonly TimeProvider clock;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly Func<double> jitter;

    public BoundedJsonClient(HttpClient client, Uri endpoint, TimeProvider clock,
        Func<TimeSpan, CancellationToken, Task>? delay = null, Func<double>? jitter = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(clock);
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttps
            || endpoint.UserInfo.Length != 0 || endpoint.Fragment.Length != 0)
            throw new ArgumentException("Use an absolute HTTPS service endpoint without credentials or fragment.", nameof(endpoint));
        if (client.Timeout != Timeout.InfiniteTimeSpan)
            throw new ArgumentException("The adapter owns the total deadline; configure an infinite client timeout.", nameof(client));
        this.client = client; this.endpoint = endpoint; this.clock = clock;
        this.delay = delay ?? ((wait, ct) => Task.Delay(wait, clock, ct));
        this.jitter = jitter ?? Random.Shared.NextDouble;
    }

    public async Task<T> GetAsync<T>(Func<Stream, T> parse, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = new CancellationTokenSource(Deadline, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var ct = linked.Token;
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                TimeSpan wait;
                CatalogFetchException failure;
                try { return await ReadOnceAsync(parse, ct).ConfigureAwait(false); }
                catch (RetryResponse retry)
                {
                    failure = new(CatalogFetchProblem.HttpStatus, retry.Status);
                    wait = retry.Wait ?? Backoff(attempt);
                }
                catch (HttpRequestException ex) when (ex.HttpRequestError is
                    HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError or HttpRequestError.ResponseEnded)
                { failure = new(CatalogFetchProblem.Network); wait = Backoff(attempt); }
                catch (IOException)
                { failure = new(CatalogFetchProblem.Network); wait = Backoff(attempt); }
                catch (HttpRequestException)
                { throw new CatalogFetchException(CatalogFetchProblem.Network); }
                // Refuse a long Retry-After rather than retrying earlier than requested.
                if (attempt + 1 >= MaximumAttempts || wait > TimeSpan.FromSeconds(5)) throw failure;
                await delay(wait, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { throw new OperationCanceledException(cancellationToken); }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        { throw new CatalogFetchException(CatalogFetchProblem.Deadline); }
        catch (OperationCanceledException)
        { throw new CatalogFetchException(CatalogFetchProblem.Network); }
    }

    private TimeSpan Backoff(int attempt)
        => TimeSpan.FromMilliseconds(200 * (1 << attempt) + Math.Clamp(jitter(), 0, 1) * 100);

    private async Task<T> ReadOnceAsync<T>(Func<Stream, T> parse, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            if ((int)response.StatusCode is 408 or 429 or 500 or 502 or 503 or 504)
            {
                var header = response.Headers.RetryAfter;
                var wait = header?.Delta ?? (header?.Date is { } date ? date - clock.GetUtcNow() : (TimeSpan?)null);
                if (wait < TimeSpan.Zero) wait = TimeSpan.Zero;
                throw new RetryResponse(response.StatusCode, wait);
            }
            throw new CatalogFetchException(CatalogFetchProblem.HttpStatus, response.StatusCode);
        }
        if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            throw new CatalogFetchException(CatalogFetchProblem.InvalidPayload);
        if (response.Content.Headers.ContentLength > MaximumBytes)
            throw new CatalogFetchException(CatalogFetchProblem.TooLarge);
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var block = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(block.AsMemory(), ct).ConfigureAwait(false);
            if (read == 0) break;
            if (buffer.Length + read > MaximumBytes)
                throw new CatalogFetchException(CatalogFetchProblem.TooLarge);
            buffer.Write(block, 0, read);
        }
        ct.ThrowIfCancellationRequested();
        buffer.Position = 0;
        try
        {
            var items = parse(buffer);
            ct.ThrowIfCancellationRequested();
            return items;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or NotSupportedException)
        { throw new CatalogFetchException(CatalogFetchProblem.InvalidPayload); }
    }

    private sealed class RetryResponse(HttpStatusCode status, TimeSpan? wait) : Exception
    {
        public HttpStatusCode Status { get; } = status;
        public TimeSpan? Wait { get; } = wait;
    }
}
