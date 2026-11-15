using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Contracts;
using GnazzosWorld.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class LocalCatalogDatabaseTests
{
    private sealed class TemporaryDatabase : IDisposable
    {
        private readonly string directory =
            Path.Combine(Path.GetTempPath(), "gnazzo-ch19-" + Guid.NewGuid().ToString("N"));
        public string PathName => Path.Combine(directory, "catalog.sqlite3");
        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static CatalogDocument Document(string revision = "sample-1")
    {
        var snapshot = CatalogSnapshot.LoadBundled();
        return new CatalogDocument(1, revision, snapshot.Items, snapshot.Creators);
    }

    [Fact]
    public async Task A_new_file_migrates_to_schema_three_and_survives_reopen()
    {
        using var folder = new TemporaryDatabase();
        using (var store = new LocalCatalogDatabase(folder.PathName))
        {
            Assert.Equal(3, await store.GetSchemaVersionAsync(ct: TestContext.Current.CancellationToken));
            Assert.Null(await store.ReadCatalogAsync(ct: TestContext.Current.CancellationToken));
            await store.ReplaceCatalogAsync(Document(), ct: TestContext.Current.CancellationToken);
            await store.SetFavoriteAsync("guest", "book-kai-01", true, ct: TestContext.Current.CancellationToken);
            await store.SetProgressAsync("guest", "book-kai-01", 35, ct: TestContext.Current.CancellationToken);
        }
        using var reopened = new LocalCatalogDatabase(folder.PathName);
        Assert.Equal(3, await reopened.GetSchemaVersionAsync(ct: TestContext.Current.CancellationToken));
        Assert.Equal("sample-1", (await reopened.ReadCatalogAsync(ct: TestContext.Current.CancellationToken))!.Revision);
        Assert.Equal(new[] { "book-kai-01" }, await reopened.ReadFavoritesAsync("guest", ct: TestContext.Current.CancellationToken));
        Assert.Equal(35, await reopened.ReadProgressAsync("guest", "book-kai-01", ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Catalog_is_atomically_replaced_and_invalid_input_does_not_destroy_previous_snapshot()
    {
        using var folder = new TemporaryDatabase();
        using var store = new LocalCatalogDatabase(folder.PathName);
        await store.ReplaceCatalogAsync(Document("before"), ct: TestContext.Current.CancellationToken);
        var snapshot = Document("invalid");
        var bad = snapshot with { Items = snapshot.Items
            .Select(item => item with { CreatorId = "missing-creator" }).ToArray() };
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReplaceCatalogAsync(bad, ct: TestContext.Current.CancellationToken));
        Assert.Equal("before", (await store.ReadCatalogAsync(ct: TestContext.Current.CancellationToken))!.Revision);
        await store.ReplaceCatalogAsync(Document("after"), ct: TestContext.Current.CancellationToken);
        Assert.Equal("after", (await store.ReadCatalogAsync(ct: TestContext.Current.CancellationToken))!.Revision);
    }

    [Fact]
    public async Task Favorites_and_progress_are_isolated_by_local_scope()
    {
        using var folder = new TemporaryDatabase();
        using var store = new LocalCatalogDatabase(folder.PathName);
        await store.SetFavoriteAsync("guest-a", "book-kai-01", true, ct: TestContext.Current.CancellationToken);
        await store.SetFavoriteAsync("guest-b", "song-1", true, ct: TestContext.Current.CancellationToken);
        await store.SetProgressAsync("guest-a", "book-kai-01", 25, ct: TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "book-kai-01" }, await store.ReadFavoritesAsync("guest-a", ct: TestContext.Current.CancellationToken));
        Assert.Equal(new[] { "song-1" }, await store.ReadFavoritesAsync("guest-b", ct: TestContext.Current.CancellationToken));
        Assert.Null(await store.ReadProgressAsync("guest-b", "book-kai-01", ct: TestContext.Current.CancellationToken));
        await store.SetFavoriteAsync("guest-a", "book-kai-01", false, ct: TestContext.Current.CancellationToken);
        Assert.Empty(await store.ReadFavoritesAsync("guest-a", ct: TestContext.Current.CancellationToken));
        Assert.Equal(25, await store.ReadProgressAsync("guest-a", "book-kai-01", ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Migrates_an_existing_v1_database_without_deleting_favorites()
    {
        using var folder = new TemporaryDatabase();
        Directory.CreateDirectory(Path.GetDirectoryName(folder.PathName)!);
        using (var connection = new SqliteConnection($"Data Source={folder.PathName};Pooling=False"))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE public_catalog (
                    singleton INTEGER PRIMARY KEY CHECK(singleton=1),
                    revision TEXT NOT NULL, payload TEXT NOT NULL);
                CREATE TABLE favorites (
                    scope TEXT NOT NULL, item_id TEXT NOT NULL,
                    PRIMARY KEY(scope, item_id));
                INSERT INTO favorites(scope, item_id) VALUES('guest', 'book-kai-01');
                PRAGMA user_version=1;
                """;
            cmd.ExecuteNonQuery();
        }
        using var store = new LocalCatalogDatabase(folder.PathName);
        Assert.Equal(3, await store.GetSchemaVersionAsync(ct: TestContext.Current.CancellationToken));
        Assert.Equal(new[] { "book-kai-01" }, await store.ReadFavoritesAsync("guest", ct: TestContext.Current.CancellationToken));
        await store.SetProgressAsync("guest", "book-kai-01", 60, ct: TestContext.Current.CancellationToken);
        Assert.Equal(60, await store.ReadProgressAsync("guest", "book-kai-01", ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Refuses_a_database_from_a_future_schema_version_without_downgrade()
    {
        using var folder = new TemporaryDatabase();
        Directory.CreateDirectory(Path.GetDirectoryName(folder.PathName)!);
        using (var connection = new SqliteConnection($"Data Source={folder.PathName};Pooling=False"))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA user_version=99;";
            cmd.ExecuteNonQuery();
        }
        using var store = new LocalCatalogDatabase(folder.PathName);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.GetSchemaVersionAsync(ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Rejected_progress_and_canceled_operation_leave_prior_value_unchanged()
    {
        using var folder = new TemporaryDatabase();
        using var store = new LocalCatalogDatabase(folder.PathName);
        await store.SetProgressAsync("guest", "book-kai-01", 42, ct: TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.SetProgressAsync("guest", "book-kai-01", 101, ct: TestContext.Current.CancellationToken));
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.SetProgressAsync("guest", "book-kai-01", 50, cancel.Token));
        Assert.Equal(42, await store.ReadProgressAsync("guest", "book-kai-01", ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_database_constraint_failure_does_not_erase_earlier_progress()
    {
        using var folder = new TemporaryDatabase();
        using var store = new LocalCatalogDatabase(folder.PathName);
        await store.SetProgressAsync("guest", "book-kai-01", 22, ct: TestContext.Current.CancellationToken);
        using (var connection = new SqliteConnection($"Data Source={folder.PathName};Pooling=False"))
        {
            connection.Open();
            using var trigger = connection.CreateCommand();
            trigger.CommandText = """
                CREATE TRIGGER reject_update BEFORE UPDATE ON reading_progress
                BEGIN SELECT RAISE(FAIL, 'simulated disk write refusal'); END;
                """;
            trigger.ExecuteNonQuery();
        }
        await Assert.ThrowsAsync<SqliteException>(() =>
            store.SetProgressAsync("guest", "book-kai-01", 80, ct: TestContext.Current.CancellationToken));
        Assert.Equal(22, await store.ReadProgressAsync("guest", "book-kai-01", ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Concurrent_writes_through_one_store_preserve_all_distinct_items()
    {
        using var folder = new TemporaryDatabase();
        using var store = new LocalCatalogDatabase(folder.PathName);
        var operations = Enumerable.Range(1, 12)
            .Select(i => store.SetFavoriteAsync("guest", $"item-{i:D2}", true, ct: TestContext.Current.CancellationToken));
        await Task.WhenAll(operations);
        Assert.Equal(12, (await store.ReadFavoritesAsync("guest", ct: TestContext.Current.CancellationToken)).Count);
    }
}
