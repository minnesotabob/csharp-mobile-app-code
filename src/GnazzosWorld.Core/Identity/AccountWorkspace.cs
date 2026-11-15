using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace GnazzosWorld.Core.Identity;

/// <summary>Local partition identity, not an authentication credential.</summary>
public sealed record AccountOwner
{
    public string Issuer { get; }
    public string Subject { get; }
    public AccountOwner(Uri issuer, string subject)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        if (!issuer.IsAbsoluteUri || issuer.Scheme != "https" ||
            issuer.AbsoluteUri.Length > 512 || issuer.UserInfo.Length != 0 ||
            issuer.Query.Length != 0 || issuer.Fragment.Length != 0)
            throw new ArgumentException("Use the validated HTTPS issuer.", nameof(issuer));
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 256 || subject.Any(char.IsControl))
            throw new ArgumentException("Use a bounded stable subject.", nameof(subject));
        Issuer = issuer.AbsoluteUri; Subject = subject;
    }
    public override string ToString() => "AccountOwner [redacted]";
}

/// <summary>An in-process handle; neither serialized nor accepted by the API.</summary>
public sealed class WorkspaceLease
{
    internal AccountOwner Owner { get; }
    internal WorkspaceLease(AccountOwner owner) => Owner = owner;
    public override string ToString() => "WorkspaceLease [redacted]";
}

public sealed record ProfilePreferences(string DisplayName, bool SyncEnabled, bool OrderNotifications);
public sealed record ProfileSnapshot(ProfilePreferences Preferences, long Revision,
    string? ConsentNotice, DateTimeOffset? ConsentUtc);
public enum LifecycleKind { Export = 1, Deletion = 2 }
public enum LifecycleState { Queued = 0, ReauthenticationRequired = 1, Accepted = 2, Completed = 3 }
public sealed record LifecycleOperation(Guid Id, LifecycleKind Kind, LifecycleState State);

/// <summary>
/// Chapter 25 local SQLite lab. One coordinator/connection; no cross-process writer contract.
/// Contains no live identity, transport, provider deletion or native account integration.
/// </summary>
public sealed class AccountWorkspace : IDisposable
{
    public const string NoticeVersion = "sync-draft-2026-11";
    private readonly SqliteConnection connection;
    private readonly TimeProvider clock;
    private readonly object gate = new();
    private WorkspaceLease? current;
    private bool disposed;
    public AccountWorkspace(string databasePath, TimeProvider clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        this.clock = clock;
        connection = new(new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS workspace_profile(
              issuer TEXT NOT NULL, subject TEXT NOT NULL,
              display_name TEXT NOT NULL, sync_enabled INTEGER NOT NULL,
              order_notifications INTEGER NOT NULL, revision INTEGER NOT NULL,
              consent_notice TEXT NULL, consent_utc TEXT NULL,
              PRIMARY KEY(issuer,subject));
            CREATE TABLE IF NOT EXISTS workspace_operation(
              issuer TEXT NOT NULL, subject TEXT NOT NULL,
              kind INTEGER NOT NULL, operation_id TEXT NOT NULL,
              state INTEGER NOT NULL,
              PRIMARY KEY(issuer,subject,kind), UNIQUE(operation_id));
            """;
        command.ExecuteNonQuery();
    }
    public WorkspaceLease Activate(AccountOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return current = new(owner);
        }
    }
    public void Detach()
    {
        lock (gate) { ObjectDisposedException.ThrowIf(disposed, this); current = null; }
    }
    public bool IsCurrent(WorkspaceLease lease)
    {
        lock (gate) return !disposed && lease is not null && ReferenceEquals(current, lease);
    }
    private AccountOwner Check(WorkspaceLease lease)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (lease is null || !ReferenceEquals(current, lease))
            throw new InvalidOperationException("Account workspace changed. Discard this result.");
        return lease.Owner;
    }
    private SqliteCommand Command(AccountOwner owner, string sql, SqliteTransaction? transaction = null)
    {
        var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$issuer", owner.Issuer);
        command.Parameters.AddWithValue("$subject", owner.Subject);
        return command;
    }
    private LifecycleOperation? Find(AccountOwner owner, LifecycleKind kind, SqliteTransaction? transaction = null)
    {
        using var command = Command(owner, """
            SELECT operation_id,state FROM workspace_operation
            WHERE issuer=$issuer AND subject=$subject AND kind=$kind
            """, transaction);
        command.Parameters.AddWithValue("$kind", (int)kind);
        using var reader = command.ExecuteReader();
        return reader.Read() ? new(Guid.Parse(reader.GetString(0)), kind, (LifecycleState)reader.GetInt32(1)) : null;
    }
    private void RequireEditable(AccountOwner owner)
    {
        if (Find(owner, LifecycleKind.Deletion) is not null)
            throw new InvalidOperationException("Removal is pending or complete for this workspace.");
    }
    private ProfileSnapshot Read(AccountOwner owner)
    {
        using var command = Command(owner, """
            SELECT display_name,sync_enabled,order_notifications,revision,consent_notice,consent_utc
            FROM workspace_profile WHERE issuer=$issuer AND subject=$subject
            """);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return new(new("", false, false), 0, null, null);
        return new(new(reader.GetString(0), reader.GetBoolean(1), reader.GetBoolean(2)), reader.GetInt64(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : DateTimeOffset.ParseExact(reader.GetString(5), "O", CultureInfo.InvariantCulture));
    }
    public ProfileSnapshot ReadProfile(WorkspaceLease lease)
    {
        lock (gate)
        {
            var owner = Check(lease);
            if (Find(owner, LifecycleKind.Deletion)?.State == LifecycleState.Completed)
                throw new InvalidOperationException("Local profile removed; only the receipt remains.");
            return Read(owner);
        }
    }
    public ProfileSnapshot SaveProfile(WorkspaceLease lease, ProfilePreferences value, long expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.DisplayName is null || value.DisplayName.Length > 120 || value.DisplayName.Any(char.IsControl))
            throw new ArgumentException("Display name must be at most 120 characters without controls.", nameof(value));
        lock (gate)
        {
            var owner = Check(lease); RequireEditable(owner);
            var before = Read(owner);
            if (before.Revision != expectedRevision) throw new InvalidOperationException("Reload the changed profile before saving.");
            var notice = value.SyncEnabled ? NoticeVersion : null;
            var consent = value.SyncEnabled
                ? (before.Preferences.SyncEnabled && before.ConsentNotice == notice ? before.ConsentUtc : clock.GetUtcNow())
                : null;
            var next = new ProfileSnapshot(value, checked(before.Revision + 1), notice, consent);
            using var command = Command(owner, """
                INSERT INTO workspace_profile VALUES($issuer,$subject,$name,$sync,$notifications,$revision,$notice,$consent)
                ON CONFLICT(issuer,subject) DO UPDATE SET display_name=$name,sync_enabled=$sync,
                  order_notifications=$notifications,revision=$revision,consent_notice=$notice,consent_utc=$consent
                """);
            command.Parameters.AddWithValue("$name", value.DisplayName);
            command.Parameters.AddWithValue("$sync", value.SyncEnabled);
            command.Parameters.AddWithValue("$notifications", value.OrderNotifications);
            command.Parameters.AddWithValue("$revision", next.Revision);
            command.Parameters.AddWithValue("$notice", (object?)notice ?? DBNull.Value);
            command.Parameters.AddWithValue("$consent", (object?)consent?.ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value);
            command.ExecuteNonQuery();
            return next;
        }
    }
    public bool CanSynchronize(WorkspaceLease lease)
    {
        lock (gate)
        {
            var owner = Check(lease); var profile = Read(owner);
            return Find(owner, LifecycleKind.Deletion) is null && profile.Preferences.SyncEnabled &&
                profile.ConsentNotice == NoticeVersion && profile.ConsentUtc is not null;
        }
    }
    /// <summary>Exports this lab's profile only, not the application's complete remote data.</summary>
    public string ExportLocalProfile(WorkspaceLease lease)
    {
        lock (gate)
        {
            var owner = Check(lease); RequireEditable(owner);
            return JsonSerializer.Serialize(new { schema = 1, scope = "chapter25-local-profile", profile = Read(owner) });
        }
    }
    public LifecycleOperation QueueRequest(WorkspaceLease lease, LifecycleKind kind)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        lock (gate)
        {
            var owner = Check(lease);
            if (kind == LifecycleKind.Export) RequireEditable(owner);
            if (Find(owner, kind) is { } existing) return existing;
            using var command = Command(owner, """
                INSERT INTO workspace_operation VALUES($issuer,$subject,$kind,$id,0)
                """);
            var id = Guid.NewGuid(); command.Parameters.AddWithValue("$kind", (int)kind);
            command.Parameters.AddWithValue("$id", id.ToString("D")); command.ExecuteNonQuery();
            return new(id, kind, LifecycleState.Queued);
        }
    }
    public LifecycleOperation? GetOperation(WorkspaceLease lease, LifecycleKind kind)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        lock (gate) return Find(Check(lease), kind);
    }
    /// <summary>
    /// Caller must validate the authenticated service receipt before applying it.
    /// The console lab supplies explicit simulated receipts, not proof of remote removal.
    /// </summary>
    public LifecycleOperation ApplyReceipt(WorkspaceLease lease, Guid operationId,
        LifecycleKind kind, LifecycleState state)
    {
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(state) || state == LifecycleState.Queued)
            throw new ArgumentOutOfRangeException(nameof(state));
        lock (gate)
        {
            var owner = Check(lease);
            var operation = Find(owner, kind) ?? throw new InvalidOperationException("Unknown operation.");
            if (operation.Id != operationId) throw new InvalidOperationException("Receipt does not match this account operation.");
            if ((int)state <= (int)operation.State) return operation; // Reordered/duplicate receipts never regress.
            using var transaction = connection.BeginTransaction();
            if (kind == LifecycleKind.Deletion && state == LifecycleState.Completed)
            {
                using var purge = Command(owner, """
                    DELETE FROM workspace_profile WHERE issuer=$issuer AND subject=$subject;
                    DELETE FROM workspace_operation WHERE issuer=$issuer AND subject=$subject AND kind=1;
                    """, transaction);
                purge.ExecuteNonQuery();
            }
            using var update = Command(owner, """
                UPDATE workspace_operation SET state=$state
                WHERE issuer=$issuer AND subject=$subject AND kind=$kind AND operation_id=$id
                """, transaction);
            update.Parameters.AddWithValue("$state", (int)state); update.Parameters.AddWithValue("$kind", (int)kind);
            update.Parameters.AddWithValue("$id", operationId.ToString("D")); update.ExecuteNonQuery(); transaction.Commit();
            return operation with { State = state };
        }
    }
    public void Dispose()
    {
        lock (gate) { if (disposed) return; current = null; disposed = true; connection.Dispose(); }
    }
}
