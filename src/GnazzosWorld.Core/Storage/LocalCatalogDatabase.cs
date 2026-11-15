using System.Text.Json;
using System.Text.Json.Serialization;
using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Contracts;
using Microsoft.Data.Sqlite;

namespace GnazzosWorld.Core.Storage;

/// <summary>
/// Chapter 19 teaching store for public catalog metadata and local anonymous state.
/// No private customer details, credentials, payments, or account authentication.
/// A single instance serializes access to its own database file.
/// </summary>
public sealed class LocalCatalogDatabase(string databasePath) : IDisposable
{
    public const int SchemaVersion = 2;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string path = Path.GetFullPath(databasePath);
    private bool ready;
    private bool disposed;

    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public Task<int> GetSchemaVersionAsync(CancellationToken ct = default)
        => InDatabaseAsync(async (db, token) =>
        {
            using var cmd = Command(db, "PRAGMA user_version;");
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(token));
        }, ct);

    /// <summary>Atomically replaces one coherent public snapshot; rejects invalid documents first.</summary>
    public Task ReplaceCatalogAsync(CatalogDocument document, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Validate(); // Includes creator links and duplicate IDs.
        var json = JsonSerializer.Serialize(document, Json);
        return InDatabaseAsync(async (db, token) =>
        {
            using var tx = db.BeginTransaction();
            using var cmd = Command(db, """
                INSERT INTO public_catalog(singleton, revision, payload)
                VALUES (1, $revision, $payload)
                ON CONFLICT(singleton) DO UPDATE SET
                    revision = excluded.revision,
                    payload = excluded.payload;
                """, tx);
            cmd.Parameters.AddWithValue("$revision", document.Revision);
            cmd.Parameters.AddWithValue("$payload", json);
            await cmd.ExecuteNonQueryAsync(token);
            token.ThrowIfCancellationRequested();
            tx.Commit();
            return true;
        }, ct);
    }

    public Task<CatalogDocument?> ReadCatalogAsync(CancellationToken ct = default)
        => InDatabaseAsync(async (db, token) =>
        {
            using var cmd = Command(db, "SELECT payload FROM public_catalog WHERE singleton = 1;");
            var payload = await cmd.ExecuteScalarAsync(token) as string;
            if (payload is null) return null;
            try
            {
                var document = JsonSerializer.Deserialize<CatalogDocument>(payload, Json)
                    ?? throw new InvalidDataException("Stored catalog is missing.");
                document.Validate();
                return document;
            }
            catch (Exception e) when (e is JsonException or NotSupportedException)
            {
                throw new InvalidDataException("Stored catalog could not be decoded.", e);
            }
        }, ct);

    public Task SetFavoriteAsync(string scope, string itemId, bool selected, CancellationToken ct = default)
    {
        CheckIdentity(scope, nameof(scope));
        CheckIdentity(itemId, nameof(itemId));
        return InDatabaseAsync(async (db, token) =>
        {
            using var cmd = selected
                ? Command(db, "INSERT OR IGNORE INTO favorites(scope, item_id) VALUES($scope, $id);")
                : Command(db, "DELETE FROM favorites WHERE scope=$scope AND item_id=$id;");
            cmd.Parameters.AddWithValue("$scope", scope);
            cmd.Parameters.AddWithValue("$id", itemId);
            await cmd.ExecuteNonQueryAsync(token);
            return true;
        }, ct);
    }

    public Task<IReadOnlyList<string>> ReadFavoritesAsync(string scope, CancellationToken ct = default)
    {
        CheckIdentity(scope, nameof(scope));
        return InDatabaseAsync(async (db, token) =>
        {
            using var cmd = Command(db,
                "SELECT item_id FROM favorites WHERE scope=$scope ORDER BY item_id COLLATE BINARY;");
            cmd.Parameters.AddWithValue("$scope", scope);
            var result = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) result.Add(reader.GetString(0));
            return (IReadOnlyList<string>)result;
        }, ct);
    }

    public Task SetProgressAsync(string scope, string itemId, int percent, CancellationToken ct = default)
    {
        CheckIdentity(scope, nameof(scope));
        CheckIdentity(itemId, nameof(itemId));
        if (percent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(percent));
        return InDatabaseAsync(async (db, token) =>
        {
            using var cmd = Command(db, """
                INSERT INTO reading_progress(scope, item_id, percent)
                VALUES ($scope, $id, $percent)
                ON CONFLICT(scope, item_id) DO UPDATE SET percent=excluded.percent;
                """);
            cmd.Parameters.AddWithValue("$scope", scope);
            cmd.Parameters.AddWithValue("$id", itemId);
            cmd.Parameters.AddWithValue("$percent", percent);
            await cmd.ExecuteNonQueryAsync(token);
            return true;
        }, ct);
    }

    public Task<int?> ReadProgressAsync(string scope, string itemId, CancellationToken ct = default)
    {
        CheckIdentity(scope, nameof(scope));
        CheckIdentity(itemId, nameof(itemId));
        return InDatabaseAsync(async (db, token) =>
        {
            using var cmd = Command(db,
                "SELECT percent FROM reading_progress WHERE scope=$scope AND item_id=$id;");
            cmd.Parameters.AddWithValue("$scope", scope);
            cmd.Parameters.AddWithValue("$id", itemId);
            var result = await cmd.ExecuteScalarAsync(token);
            return result is null or DBNull ? (int?)null : Convert.ToInt32(result);
        }, ct);
    }

    private async Task<T> InDatabaseAsync<T>(
        Func<SqliteConnection, CancellationToken, Task<T>> operation,
        CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await gate.WaitAsync(ct);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            };
            using var db = new SqliteConnection(builder.ToString());
            await db.OpenAsync(ct);
            if (!ready)
            {
                Migrate(db, ct);
                ready = true;
            }
            return await operation(db, ct);
        }
        finally { gate.Release(); }
    }

    private static SqliteCommand Command(SqliteConnection db, string sql, SqliteTransaction? tx = null)
    {
        var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = tx;
        return cmd;
    }

    private static void Migrate(SqliteConnection db, CancellationToken ct)
    {
        using var get = Command(db, "PRAGMA user_version;");
        var version = Convert.ToInt32(get.ExecuteScalar());
        if (version > SchemaVersion || version < 0)
            throw new InvalidDataException($"Unsupported future database schema {version}.");
        if (version == SchemaVersion) return;

        // Versions 0 -> 1 -> 2 migrate as one SQLite transaction.
        using var tx = db.BeginTransaction();
        if (version == 0)
        {
            using var initial = Command(db, """
                CREATE TABLE public_catalog (
                    singleton INTEGER PRIMARY KEY CHECK(singleton=1),
                    revision TEXT NOT NULL,
                    payload TEXT NOT NULL
                );
                CREATE TABLE favorites (
                    scope TEXT NOT NULL,
                    item_id TEXT NOT NULL,
                    PRIMARY KEY(scope, item_id)
                );
                PRAGMA user_version=1;
                """, tx);
            ct.ThrowIfCancellationRequested();
            initial.ExecuteNonQuery();
            version = 1;
        }
        if (version == 1)
        {
            using var upgrade = Command(db, """
                CREATE TABLE reading_progress (
                    scope TEXT NOT NULL,
                    item_id TEXT NOT NULL,
                    percent INTEGER NOT NULL CHECK(percent BETWEEN 0 AND 100),
                    PRIMARY KEY(scope, item_id)
                );
                PRAGMA user_version=2;
                """, tx);
            ct.ThrowIfCancellationRequested();
            upgrade.ExecuteNonQuery();
        }
        ct.ThrowIfCancellationRequested();
        tx.Commit();
    }

    private static void CheckIdentity(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 ||
            value.Any(char.IsControl))
            throw new ArgumentException("Use a short, nonempty, non-control identifier.", parameter);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        gate.Dispose();
    }
}
