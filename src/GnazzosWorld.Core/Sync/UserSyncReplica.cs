using Microsoft.Data.Sqlite;

namespace GnazzosWorld.Core.Sync;

/// <summary>Disposable teaching replica. One owning instance per file; not a native UI adapter.</summary>
public sealed class UserSyncReplica
{
    private readonly string connectionString;
    private readonly object gate = new();
    private readonly SemaphoreSlim worker = new(1, 1);
    public string Account { get; }

    public UserSyncReplica(string path, string account)
    {
        SyncRules.Identity(account);
        Account = account;
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = full }.ToString();
        using var db = Open();
        using var cmd = Command(db, """
            CREATE TABLE IF NOT EXISTS replica_meta (
                singleton INTEGER PRIMARY KEY CHECK(singleton=1),
                account TEXT NOT NULL, cursor INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS user_rows (
                kind INTEGER NOT NULL, item_id TEXT NOT NULL,
                version INTEGER NOT NULL, value TEXT NULL,
                PRIMARY KEY(kind,item_id));
            CREATE TABLE IF NOT EXISTS outbox (
                kind INTEGER NOT NULL, item_id TEXT NOT NULL,
                operation TEXT NOT NULL UNIQUE, base_version INTEGER NOT NULL,
                value TEXT NULL, conflict INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY(kind,item_id));
            """);
        cmd.ExecuteNonQuery();
        using var insert = Command(db, "INSERT OR IGNORE INTO replica_meta VALUES(1,$a,0);", ("$a", account));
        insert.ExecuteNonQuery();
        using var owner = Command(db, "SELECT account FROM replica_meta WHERE singleton=1;");
        if (!string.Equals((string?)owner.ExecuteScalar(), account, StringComparison.Ordinal))
            throw new InvalidOperationException("A replica file belongs to one simulated account.");
    }
    private SqliteConnection Open()
    {
        var db = new SqliteConnection(connectionString);
        try { db.Open(); return db; }
        catch { db.Dispose(); throw; }
    }
    private static SqliteCommand Command(SqliteConnection db, string sql, params (string, object?)[] args)
    {
        var cmd = db.CreateCommand(); cmd.CommandText = sql;
        foreach (var (key, value) in args) cmd.Parameters.AddWithValue(key, value ?? DBNull.Value);
        return cmd;
    }
    private static UserDataRow ReadRow(SqliteConnection db, UserDataKey key)
    {
        using var cmd = Command(db, "SELECT version,value FROM user_rows WHERE kind=$k AND item_id=$i;",
            ("$k", (int)key.Kind), ("$i", key.ItemId));
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? new(key, reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetString(1)) : new(key, 0, null);
    }
    private static void SaveRow(SqliteConnection db, SqliteTransaction tx, UserDataRow row)
    {
        using var cmd = Command(db, """
            INSERT INTO user_rows VALUES($k,$i,$v,$x)
            ON CONFLICT(kind,item_id) DO UPDATE SET version=excluded.version,value=excluded.value
            WHERE excluded.version>user_rows.version;
            """, ("$k", (int)row.Key.Kind), ("$i", row.Key.ItemId), ("$v", row.Version), ("$x", row.Value));
        cmd.Transaction = tx; cmd.ExecuteNonQuery();
    }
    public Guid Queue(UserDataKey key, string? value)
    {
        SyncRules.Value(key, value);
        lock (gate)
        {
            using var db = Open();
            var current = ReadRow(db, key);
            var operation = Guid.NewGuid();
            using var cmd = Command(db, "INSERT INTO outbox(kind,item_id,operation,base_version,value) VALUES($k,$i,$o,$v,$x);",
                ("$k", (int)key.Kind), ("$i", key.ItemId), ("$o", operation.ToString("N")), ("$v", current.Version), ("$x", value));
            // The primary key deliberately rejects a second edit until this item is settled.
            cmd.ExecuteNonQuery();
            return operation;
        }
    }
    public ReplicaView View(UserDataKey key)
    {
        SyncRules.Value(key, null);
        lock (gate)
        {
            using var db = Open(); var row = ReadRow(db, key);
            using var cmd = Command(db, "SELECT value,conflict FROM outbox WHERE kind=$k AND item_id=$i;",
                ("$k", (int)key.Kind), ("$i", key.ItemId));
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? new(reader.IsDBNull(0) ? null : reader.GetString(0), row.Value, row.Version, true, reader.GetInt64(1) != 0)
                : new(row.Value, row.Value, row.Version, false, false);
        }
    }
    public long Cursor
    {
        get { lock (gate) { using var db = Open(); using var cmd = Command(db, "SELECT cursor FROM replica_meta WHERE singleton=1;"); return (long)cmd.ExecuteScalar()!; } }
    }
    private UserEdit[] Pending()
    {
        lock (gate)
        {
            using var db = Open(); using var cmd = Command(db,
                "SELECT operation,kind,item_id,base_version,value FROM outbox WHERE conflict=0 ORDER BY rowid LIMIT 32;");
            using var reader = cmd.ExecuteReader(); var edits = new List<UserEdit>();
            while (reader.Read()) edits.Add(new(Guid.ParseExact(reader.GetString(0), "N"),
                new((UserDataKind)reader.GetInt32(1), reader.GetString(2)), reader.GetInt64(3), reader.IsDBNull(4) ? null : reader.GetString(4)));
            return edits.ToArray();
        }
    }
    private void Acknowledge(UserEdit edit, EditReply reply)
    {
        if (reply.Account != Account || reply.Current.Key != edit.Key || reply.Current.Version < 0 || (!reply.Accepted && reply.Current.Version <= edit.ExpectedVersion) ||
            (reply.Accepted && (reply.Current.Version <= edit.ExpectedVersion || reply.Current.Value != edit.Value)))
            throw new InvalidDataException("Invalid synchronization receipt.");
        SyncRules.Value(reply.Current.Key, reply.Current.Value);
        lock (gate)
        {
            using var db = Open(); using var tx = db.BeginTransaction();
            SaveRow(db, tx, reply.Current);
            using var cmd = Command(db, reply.Accepted ? "DELETE FROM outbox WHERE operation=$o;" : "UPDATE outbox SET conflict=1 WHERE operation=$o;",
                ("$o", edit.OperationId.ToString("N")));
            cmd.Transaction = tx; cmd.ExecuteNonQuery(); tx.Commit();
        }
    }
    private void ApplyPage(ChangePage page)
    {
        if (page.Account != Account || page.After < 0 || page.Rows.Count > 100 ||
            page.Next != (page.Rows.Count == 0 ? page.After : page.Rows[^1].Version) || (page.HasMore && page.Rows.Count == 0))
            throw new InvalidDataException("Invalid account or cursor envelope.");
        var previous = page.After;
        foreach (var row in page.Rows)
        {
            SyncRules.Value(row.Key, row.Value);
            if (row.Version != previous + 1) throw new InvalidDataException("Unordered change page.");
            previous = row.Version;
        }
        lock (gate)
        {
            using var db = Open(); using var tx = db.BeginTransaction();
            using var read = Command(db, "SELECT cursor FROM replica_meta WHERE singleton=1;"); read.Transaction = tx;
            if ((long)read.ExecuteScalar()! != page.After) throw new InvalidOperationException("Cursor changed during pull.");
            foreach (var row in page.Rows) SaveRow(db, tx, row);
            using var advance = Command(db, "UPDATE replica_meta SET cursor=$c WHERE singleton=1;", ("$c", page.Next));
            advance.Transaction = tx; advance.ExecuteNonQuery(); tx.Commit();
        }
    }
    // Resolution waits for the worker, so it cannot replace an in-flight operation.
    public async Task ResolveAsync(UserDataKey key, bool keepLocal, CancellationToken ct = default)
    {
        await worker.WaitAsync(ct);
        try
        {
            lock (gate)
            {
                using var db = Open(); var current = ReadRow(db, key);
                using var cmd = Command(db, keepLocal ? """
                    UPDATE outbox SET operation=$o,base_version=$v,conflict=0
                    WHERE kind=$k AND item_id=$i AND conflict=1;
                    """ : "DELETE FROM outbox WHERE kind=$k AND item_id=$i AND conflict=1;",
                    ("$k", (int)key.Kind), ("$i", key.ItemId), ("$o", Guid.NewGuid().ToString("N")), ("$v", current.Version));
                if (cmd.ExecuteNonQuery() != 1) throw new InvalidOperationException("No conflict to resolve.");
            }
        }
        finally { worker.Release(); }
    }
    public async Task<SyncPass> SyncAsync(IUserSyncTransport transport, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transport);
        if (transport.Account != Account) throw new InvalidOperationException("Transport account does not own this replica.");
        await worker.WaitAsync(ct);
        try
        {
            var accepted = 0; var conflicts = 0;
            foreach (var edit in Pending())
            {
                ct.ThrowIfCancellationRequested();
                var reply = await transport.PushAsync(edit, ct);
                ct.ThrowIfCancellationRequested(); // A lost acknowledgment leaves the same operation queued.
                Acknowledge(edit, reply);
                if (reply.Accepted) accepted++; else conflicts++;
            }
            var page = await transport.PullAsync(Cursor, 100, ct);
            ct.ThrowIfCancellationRequested();
            ApplyPage(page);
            return new(accepted, conflicts, page.Rows.Count, page.HasMore);
        }
        finally { worker.Release(); }
    }
}
