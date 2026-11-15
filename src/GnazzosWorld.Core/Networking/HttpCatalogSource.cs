using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GnazzosWorld.Core.Catalog;

namespace GnazzosWorld.Core.Networking;

public interface ICatalogSource
{
    Task<IReadOnlyList<CatalogItem>> LoadAsync(CancellationToken cancellationToken = default);
}

public enum CatalogFetchProblem { Network, Deadline, HttpStatus, InvalidPayload, TooLarge }
public sealed class CatalogFetchException(CatalogFetchProblem problem, HttpStatusCode? status = null)
    : Exception($"Catalog fetch failed: {problem}.")
{
    public CatalogFetchProblem Problem { get; } = problem;
    public HttpStatusCode? Status { get; } = status;
}

// Chapter 17 items-only contract, retained while Chapter 18 adds a versioned snapshot.
public sealed class HttpCatalogSource : ICatalogSource
{
    public const int MaximumBytes = BoundedJsonClient.MaximumBytes;
    public const int MaximumAttempts = BoundedJsonClient.MaximumAttempts;
    public static readonly TimeSpan Deadline = BoundedJsonClient.Deadline;
    private readonly BoundedJsonClient transport;
    public HttpCatalogSource(HttpClient client, Uri endpoint, TimeProvider clock,
        Func<TimeSpan, CancellationToken, Task>? delay = null, Func<double>? jitter = null)
        => transport = new(client, endpoint, clock, delay, jitter);
    public Task<IReadOnlyList<CatalogItem>> LoadAsync(CancellationToken cancellationToken = default)
        => transport.GetAsync(CatalogLoader.Load, cancellationToken);
}
