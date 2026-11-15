namespace GnazzosWorld.Core.Sync;

/// <summary>In-memory protocol fixture. No HTTP endpoint, authentication or durable server store.</summary>
public sealed class SimulatedSyncServer
{
    private sealed class AccountState
    {
        internal readonly Dictionary<UserDataKey, UserDataRow> Rows = [];
        internal readonly Dictionary<Guid, (UserEdit Edit, EditReply Reply)> Receipts = [];
        internal readonly List<UserDataRow> Changes = [];
    }
    private readonly Dictionary<string, AccountState> accounts = new(StringComparer.Ordinal);
    private readonly object gate = new();

    public IUserSyncTransport ForSimulatedUser(string account)
    {
        SyncRules.Identity(account);
        return new Transport(this, account);
    }
    private AccountState State(string account)
    {
        if (!accounts.TryGetValue(account, out var state)) accounts[account] = state = new();
        return state;
    }
    private EditReply Push(string account, UserEdit edit, CancellationToken ct)
    {
        SyncRules.Edit(edit);
        lock (gate)
        {
            ct.ThrowIfCancellationRequested();
            var state = State(account);
            if (state.Receipts.TryGetValue(edit.OperationId, out var prior))
            {
                if (prior.Edit != edit) throw new InvalidOperationException("Operation ID reused with different content.");
                return prior.Reply;
            }
            var current = state.Rows.GetValueOrDefault(edit.Key) ?? new(edit.Key, 0, null);
            if (current.Version < edit.ExpectedVersion)
                throw new InvalidDataException("Server history moved backward; explicit rebootstrap required.");
            var accepted = current.Version == edit.ExpectedVersion;
            if (accepted)
            {
                current = new(edit.Key, state.Changes.Count + 1L, edit.Value);
                state.Rows[edit.Key] = current;
                state.Changes.Add(current);
            }
            var reply = new EditReply(account, accepted, current);
            state.Receipts.Add(edit.OperationId, (edit, reply));
            return reply;
        }
    }
    private ChangePage Pull(string account, long after, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        lock (gate)
        {
            ct.ThrowIfCancellationRequested();
            var state = State(account);
            if (after < 0 || after > state.Changes.Count)
                throw new InvalidDataException("Cursor is outside the fixture history; explicit rebootstrap required.");
            var rows = state.Changes.Skip(checked((int)after)).Take(limit).ToArray();
            var next = rows.Length == 0 ? after : rows[^1].Version;
            return new(account, after, next, rows, next < state.Changes.Count);
        }
    }
    private sealed class Transport(SimulatedSyncServer server, string account) : IUserSyncTransport
    {
        public string Account => account;
        public Task<EditReply> PushAsync(UserEdit edit, CancellationToken ct)
            => Task.FromResult(server.Push(account, edit, ct));
        public Task<ChangePage> PullAsync(long after, int limit, CancellationToken ct)
            => Task.FromResult(server.Pull(account, after, limit, ct));
    }
}
