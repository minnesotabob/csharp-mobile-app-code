using System.Globalization;
using System.Text.Json;
using GnazzosWorld.Core.Contracts;

namespace GnazzosWorld.Core.Storage;

public sealed record CachedCatalog(CatalogDocument Document, DateTimeOffset? FetchedUtc);

public sealed partial class LocalCatalogDatabase
{
    // Coalesce requests into one durable GET intent. A new ticket protects requests
    // made while a worker is fetching: completing an older ticket cannot erase it.
    public Task RequestCatalogRefreshAsync(CancellationToken ct = default)
        => InDatabaseAsync(async (db, token) =>
        {
            using var cmd = Command(db, """
                INSERT INTO pending_refresh(singleton, ticket) VALUES(1, $ticket)
                ON CONFLICT(singleton) DO UPDATE SET ticket=excluded.ticket;
                """);
            cmd.Parameters.AddWithValue("$ticket", Guid.NewGuid().ToString("N"));
            await cmd.ExecuteNonQueryAsync(token);
            return true;
        }, ct);

    public Task<string?> ReadPendingRefreshAsync(CancellationToken ct = default)
        => InDatabaseAsync(async (db, token) =>
        {
            using var cmd = Command(db, "SELECT ticket FROM pending_refresh WHERE singleton=1;");
            return await cmd.ExecuteScalarAsync(token) as string;
        }, ct);

    public Task<CachedCatalog?> ReadCachedCatalogAsync(CancellationToken ct = default)
        => InDatabaseAsync(async (db, token) =>
        {
            using var cmd = Command(db, "SELECT payload, fetched_utc FROM public_catalog WHERE singleton=1;");
            await using var reader = await cmd.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) return null;
            using var json = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(reader.GetString(0)));
            var document = CatalogDocument.Read(json);
            DateTimeOffset? fetched = null;
            if (!reader.IsDBNull(1))
            {
                if (!DateTimeOffset.TryParseExact(reader.GetString(1), "O", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var value))
                    throw new InvalidDataException("Stored freshness timestamp is invalid.");
                fetched = value;
            }
            return new CachedCatalog(document, fetched);
        }, ct);

    public Task CommitCatalogRefreshAsync(string ticket, CatalogDocument document,
        DateTimeOffset fetchedUtc, CancellationToken ct = default)
    {
        CheckIdentity(ticket, nameof(ticket));
        ArgumentNullException.ThrowIfNull(document);
        document.Validate();
        var payload = JsonSerializer.Serialize(document, Json);
        return InDatabaseAsync(async (db, token) =>
        {
            using var tx = db.BeginTransaction();
            using var save = Command(db, """
                INSERT INTO public_catalog(singleton, revision, payload, fetched_utc)
                VALUES(1, $revision, $payload, $fetched)
                ON CONFLICT(singleton) DO UPDATE SET revision=excluded.revision,
                    payload=excluded.payload, fetched_utc=excluded.fetched_utc;
                """, tx);
            save.Parameters.AddWithValue("$revision", document.Revision);
            save.Parameters.AddWithValue("$payload", payload);
            save.Parameters.AddWithValue("$fetched", fetchedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            await save.ExecuteNonQueryAsync(token);
            using var complete = Command(db,
                "DELETE FROM pending_refresh WHERE singleton=1 AND ticket=$ticket;", tx);
            complete.Parameters.AddWithValue("$ticket", ticket);
            await complete.ExecuteNonQueryAsync(token);
            token.ThrowIfCancellationRequested();
            tx.Commit();
            return true;
        }, ct);
    }
}
