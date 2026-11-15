using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Contracts;
using GnazzosWorld.Core.Networking;
using GnazzosWorld.Core.Storage;

namespace GnazzosWorld.Core.Offline;

public enum CatalogOrigin { Bundled, Saved, Refreshed }
public sealed record OfflineCatalogResult(CatalogDocument Document, CatalogOrigin Origin,
    DateTimeOffset? FetchedUtc, bool IsStale, CatalogFetchProblem? RefreshProblem = null);

// One application-owned coordinator/worker per store. No OS scheduler or order writes.
public sealed class OfflineCatalogService(LocalCatalogDatabase database,
    Func<CancellationToken, Task<CatalogDocument>> fetch, TimeProvider clock)
{
    public static readonly TimeSpan FreshFor = TimeSpan.FromHours(24);
    private readonly SemaphoreSlim worker = new(1, 1);

    public async Task<OfflineCatalogResult> ReadLocalAsync(CancellationToken ct = default)
    {
        var saved = await database.ReadCachedCatalogAsync(ct);
        if (saved is not null) return Result(saved, CatalogOrigin.Saved);
        var bundled = CatalogSnapshot.LoadBundled();
        return new(new CatalogDocument(1, "bundled", bundled.Items, bundled.Creators),
            CatalogOrigin.Bundled, null, true);
    }

    // Caller explicitly persists intent first, then runs one bounded attempt.
    // Cancellation escapes; only classified catalog transport failures produce fallback.
    public async Task<OfflineCatalogResult> RunPendingAsync(CancellationToken ct = default)
    {
        await worker.WaitAsync(ct);
        try
        {
            var ticket = await database.ReadPendingRefreshAsync(ct);
            if (ticket is null) return await ReadLocalAsync(ct);
            CatalogDocument document;
            try { document = await fetch(ct); }
            catch (CatalogFetchException e)
            {
                ct.ThrowIfCancellationRequested();
                return (await ReadLocalAsync(ct)) with { RefreshProblem = e.Problem };
            }
            ct.ThrowIfCancellationRequested();
            var fetched = clock.GetUtcNow();
            await database.CommitCatalogRefreshAsync(ticket, document, fetched, ct);
            return Result(new CachedCatalog(document, fetched), CatalogOrigin.Refreshed);
        }
        finally { worker.Release(); }
    }

    private OfflineCatalogResult Result(CachedCatalog saved, CatalogOrigin origin)
    {
        var age = saved.FetchedUtc is { } fetched ? clock.GetUtcNow() - fetched : (TimeSpan?)null;
        // Unknown and future timestamps are untrusted, including after clock correction.
        var stale = age is null || age.Value < TimeSpan.Zero || age.Value >= FreshFor;
        return new(saved.Document, origin, saved.FetchedUtc, stale);
    }
}
