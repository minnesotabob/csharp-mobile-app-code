using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Contracts;
using GnazzosWorld.Core.Networking;
using GnazzosWorld.Core.Offline;
using GnazzosWorld.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class OfflineCatalogTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "gnazzo-ch20-" + Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(directory, "catalog.sqlite3");
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private static CatalogDocument Document(string revision = "sample-20")
    {
        var bundled = CatalogSnapshot.LoadBundled();
        return new(1, revision, bundled.Items, bundled.Creators);
    }
    private static OfflineCatalogService Service(LocalCatalogDatabase db, Clock clock,
        Func<CancellationToken, Task<CatalogDocument>>? fetch = null)
        => new(db, fetch ?? (_ => Task.FromResult(Document())), clock);

    [Fact]
    public async Task Empty_store_uses_bundle_without_fetching()
    {
        using var db = new LocalCatalogDatabase(DatabasePath);
        var calls = 0;
        var service = Service(db, new(), _ => { calls++; return Task.FromResult(Document()); });
        var result = await service.ReadLocalAsync(ct: TestContext.Current.CancellationToken);
        Assert.Equal(CatalogOrigin.Bundled, result.Origin);
        Assert.True(result.IsStale);
        Assert.Null(result.FetchedUtc);
        await service.RunPendingAsync(ct: TestContext.Current.CancellationToken);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1439, false)]
    [InlineData(1440, true)]
    public async Task Freshness_boundary_and_future_clock_are_explicit(int minutes, bool stale)
    {
        using var db = new LocalCatalogDatabase(DatabasePath);
        var clock = new Clock();
        await db.CommitCatalogRefreshAsync("fixture", Document(), clock.Now, ct: TestContext.Current.CancellationToken);
        clock.Now = clock.Now.AddMinutes(minutes);
        Assert.Equal(stale, (await Service(db, clock).ReadLocalAsync(ct: TestContext.Current.CancellationToken)).IsStale);
    }

    [Fact]
    public async Task Legacy_replacement_has_unknown_freshness_and_clears_prior_timestamp()
    {
        using var db = new LocalCatalogDatabase(DatabasePath);
        var clock = new Clock();
        await db.CommitCatalogRefreshAsync("fixture", Document(), clock.Now, ct: TestContext.Current.CancellationToken);
        await db.ReplaceCatalogAsync(Document("legacy"), ct: TestContext.Current.CancellationToken);
        var result = await Service(db, clock).ReadLocalAsync(ct: TestContext.Current.CancellationToken);
        Assert.Equal("legacy", result.Document.Revision);
        Assert.Null(result.FetchedUtc);
        Assert.True(result.IsStale);
    }

    [Fact]
    public async Task Pending_intent_survives_reopen_and_success_commits_snapshot_and_acknowledgment()
    {
        using (var first = new LocalCatalogDatabase(DatabasePath))
            await first.RequestCatalogRefreshAsync(ct: TestContext.Current.CancellationToken);
        using var reopened = new LocalCatalogDatabase(DatabasePath);
        var result = await Service(reopened, new()).RunPendingAsync(ct: TestContext.Current.CancellationToken);
        Assert.Equal(CatalogOrigin.Refreshed, result.Origin);
        Assert.False(result.IsStale);
        Assert.Null(await reopened.ReadPendingRefreshAsync(ct: TestContext.Current.CancellationToken));
        Assert.Equal("sample-20", (await reopened.ReadCachedCatalogAsync(ct: TestContext.Current.CancellationToken))!.Document.Revision);
    }

    [Fact]
    public async Task Failed_fetch_preserves_old_snapshot_and_pending_intent()
    {
        using var db = new LocalCatalogDatabase(DatabasePath);
        await db.ReplaceCatalogAsync(Document("before"), ct: TestContext.Current.CancellationToken);
        await db.RequestCatalogRefreshAsync(ct: TestContext.Current.CancellationToken);
        var service = Service(db, new(), _ => throw new CatalogFetchException(CatalogFetchProblem.Network));
        var result = await service.RunPendingAsync(ct: TestContext.Current.CancellationToken);
        Assert.Equal("before", result.Document.Revision);
        Assert.Equal(CatalogFetchProblem.Network, result.RefreshProblem);
        Assert.NotNull(await db.ReadPendingRefreshAsync(ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Cancellation_during_fetch_keeps_intent_and_propagates()
    {
        using var db = new LocalCatalogDatabase(DatabasePath);
        await db.RequestCatalogRefreshAsync(ct: TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        var service = Service(db, new(), token => { cts.Cancel(); token.ThrowIfCancellationRequested(); return Task.FromResult(Document()); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RunPendingAsync(cts.Token));
        Assert.NotNull(await db.ReadPendingRefreshAsync(ct: TestContext.Current.CancellationToken));
        Assert.Null(await db.ReadCachedCatalogAsync(ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Request_arriving_during_fetch_is_not_erased_by_older_acknowledgment()
    {
        using var db = new LocalCatalogDatabase(DatabasePath);
        await db.RequestCatalogRefreshAsync(ct: TestContext.Current.CancellationToken);
        var oldTicket = await db.ReadPendingRefreshAsync(ct: TestContext.Current.CancellationToken);
        var service = Service(db, new(), async _ => { await db.RequestCatalogRefreshAsync(ct: TestContext.Current.CancellationToken); return Document(); });
        await service.RunPendingAsync(ct: TestContext.Current.CancellationToken);
        Assert.NotNull(await db.ReadPendingRefreshAsync(ct: TestContext.Current.CancellationToken));
        Assert.NotEqual(oldTicket, await db.ReadPendingRefreshAsync(ct: TestContext.Current.CancellationToken));
        await Service(db, new()).RunPendingAsync(ct: TestContext.Current.CancellationToken);
        Assert.Null(await db.ReadPendingRefreshAsync(ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Failure_between_save_and_acknowledgment_rolls_back_both()
    {
        using var db = new LocalCatalogDatabase(DatabasePath);
        await db.ReplaceCatalogAsync(Document("before"), ct: TestContext.Current.CancellationToken);
        await db.RequestCatalogRefreshAsync(ct: TestContext.Current.CancellationToken);
        using (var connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False"))
        {
            connection.Open();
            using var trigger = connection.CreateCommand();
            trigger.CommandText = """
                CREATE TRIGGER reject_ack BEFORE DELETE ON pending_refresh
                BEGIN SELECT RAISE(FAIL, 'simulated acknowledgment failure'); END;
                """;
            trigger.ExecuteNonQuery();
        }
        await Assert.ThrowsAsync<SqliteException>(() => Service(db, new()).RunPendingAsync(ct: TestContext.Current.CancellationToken));
        Assert.Equal("before", (await db.ReadCatalogAsync(ct: TestContext.Current.CancellationToken))!.Revision);
        Assert.NotNull(await db.ReadPendingRefreshAsync(ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Version_two_upgrade_preserves_catalog_favorites_and_progress()
    {
        Directory.CreateDirectory(directory);
        using (var connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False"))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE public_catalog(singleton INTEGER PRIMARY KEY, revision TEXT NOT NULL, payload TEXT NOT NULL);
                CREATE TABLE favorites(scope TEXT NOT NULL,item_id TEXT NOT NULL,PRIMARY KEY(scope,item_id));
                CREATE TABLE reading_progress(scope TEXT NOT NULL,item_id TEXT NOT NULL,percent INTEGER NOT NULL,PRIMARY KEY(scope,item_id));
                INSERT INTO favorites VALUES('guest','book-kai-01');
                INSERT INTO reading_progress VALUES('guest','book-kai-01',35);
                PRAGMA user_version=2;
                """;
            cmd.ExecuteNonQuery();
        }
        using var db = new LocalCatalogDatabase(DatabasePath);
        Assert.Equal(3, await db.GetSchemaVersionAsync(ct: TestContext.Current.CancellationToken));
        Assert.Equal(new[] { "book-kai-01" }, await db.ReadFavoritesAsync("guest", ct: TestContext.Current.CancellationToken));
        Assert.Equal(35, await db.ReadProgressAsync("guest", "book-kai-01", ct: TestContext.Current.CancellationToken));
        await db.RequestCatalogRefreshAsync(ct: TestContext.Current.CancellationToken);
        Assert.NotNull(await db.ReadPendingRefreshAsync(ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Concurrent_workers_on_one_coordinator_fetch_once()
    {
        using var db = new LocalCatalogDatabase(DatabasePath);
        await db.RequestCatalogRefreshAsync(ct: TestContext.Current.CancellationToken);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var service = Service(db, new(), async _ => { calls++; entered.SetResult(true); await release.Task; return Document(); });
        var first = service.RunPendingAsync(ct: TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var second = service.RunPendingAsync(ct: TestContext.Current.CancellationToken);
        release.SetResult(true);
        await Task.WhenAll(first, second);
        Assert.Equal(1, calls);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
