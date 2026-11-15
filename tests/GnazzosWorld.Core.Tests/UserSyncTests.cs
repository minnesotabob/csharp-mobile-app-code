using GnazzosWorld.Core.Sync;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class UserSyncTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "gnazzo-ch21-" + Guid.NewGuid().ToString("N"));
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly UserDataKey Song = new(UserDataKind.Favorite, "song-northern-lights");
    private static readonly UserDataKey Draft = new(UserDataKind.Draft, "anniversary-demo");
    private UserSyncReplica Replica(string device = "phone", string account = "demo-reader")
        => new(Path.Combine(directory, device + ".sqlite3"), account);
    private sealed class Intercept(IUserSyncTransport inner) : IUserSyncTransport
    {
        public string Account => inner.Account;
        public Func<UserEdit, CancellationToken, Task<EditReply>>? Push { get; init; }
        public Func<long, int, CancellationToken, Task<ChangePage>>? Pull { get; init; }
        public Task<EditReply> PushAsync(UserEdit edit, CancellationToken ct)
            => Push is null ? inner.PushAsync(edit, ct) : Push(edit, ct);
        public Task<ChangePage> PullAsync(long after, int limit, CancellationToken ct)
            => Pull is null ? inner.PullAsync(after, limit, ct) : Pull(after, limit, ct);
    }

    [Theory]
    [InlineData(UserDataKind.Favorite, "saved")]
    [InlineData(UserDataKind.Bookmark, "sample-section-3")]
    [InlineData(UserDataKind.Progress, "37")]
    [InlineData(UserDataKind.Draft, "Fictitious anniversary song idea")]
    public async Task Four_value_kinds_reach_a_second_device(UserDataKind kind, string value)
    {
        var server = new SimulatedSyncServer(); var transport = server.ForSimulatedUser("demo-reader");
        var key = new UserDataKey(kind, "fixture-item");
        var phone = Replica(); var tablet = Replica("tablet");
        phone.Queue(key, value);
        Assert.Equal(1, (await phone.SyncAsync(transport, Ct)).Accepted);
        await tablet.SyncAsync(transport, Ct);
        Assert.Equal(value, tablet.View(key).Value);
        Assert.False(tablet.View(key).Pending);
        Assert.Equal(1, tablet.Cursor);
    }
    [Fact]
    public async Task Lost_acknowledgment_reopens_and_replays_one_operation()
    {
        var server = new SimulatedSyncServer(); var transport = server.ForSimulatedUser("demo-reader");
        var phone = Replica(); var id = phone.Queue(Song, "saved");
        var dropping = new Intercept(transport) { Push = async (edit, ct) =>
        {
            Assert.Equal(id, edit.OperationId);
            await transport.PushAsync(edit, ct);
            throw new IOException("Fixture dropped reply after commit.");
        }};
        await Assert.ThrowsAsync<IOException>(() => phone.SyncAsync(dropping, Ct));
        var reopened = Replica(); Assert.True(reopened.View(Song).Pending);
        Assert.Equal(1, (await reopened.SyncAsync(transport, Ct)).Accepted);
        Assert.False(reopened.View(Song).Pending);
        Assert.Equal(1, reopened.Cursor); // Duplicate delivery did not add a second event.
    }
    [Fact]
    public async Task Concurrent_drafts_keep_local_text_until_explicit_resolution()
    {
        var server = new SimulatedSyncServer(); var transport = server.ForSimulatedUser("demo-reader");
        var phone = Replica(); var tablet = Replica("tablet");
        phone.Queue(Draft, "Phone verse"); tablet.Queue(Draft, "Tablet chorus");
        await phone.SyncAsync(transport, Ct);
        Assert.Equal(1, (await tablet.SyncAsync(transport, Ct)).Conflicts);
        var view = tablet.View(Draft);
        Assert.True(view.Conflict); Assert.Equal("Tablet chorus", view.Value); Assert.Equal("Phone verse", view.ServerValue);
        await tablet.ResolveAsync(Draft, keepLocal: true, Ct);
        await tablet.SyncAsync(transport, Ct);
        await phone.SyncAsync(transport, Ct);
        Assert.Equal("Tablet chorus", phone.View(Draft).Value);
        Assert.False(tablet.View(Draft).Pending);
    }
    [Fact]
    public async Task Accepting_remote_conflict_discards_only_the_conflicting_local_edit()
    {
        var transport = new SimulatedSyncServer().ForSimulatedUser("demo-reader");
        var phone = Replica(); var tablet = Replica("tablet");
        phone.Queue(Draft, "Server choice"); tablet.Queue(Draft, "Local choice");
        tablet.Queue(Song, "saved");
        await phone.SyncAsync(transport, Ct); await tablet.SyncAsync(transport, Ct);
        await tablet.ResolveAsync(Draft, keepLocal: false, Ct);
        Assert.Equal("Server choice", tablet.View(Draft).Value);
        Assert.False(tablet.View(Draft).Pending); Assert.Equal("saved", tablet.View(Song).Value);
    }
    [Fact]
    public async Task Tombstone_prevents_an_unseen_delete_from_being_resurrected()
    {
        var transport = new SimulatedSyncServer().ForSimulatedUser("demo-reader");
        var phone = Replica(); var tablet = Replica("tablet");
        phone.Queue(Song, "saved"); await phone.SyncAsync(transport, Ct); await tablet.SyncAsync(transport, Ct);
        tablet.Queue(Song, "saved"); // Based on version 1, before observing deletion.
        phone.Queue(Song, null); await phone.SyncAsync(transport, Ct);
        Assert.Equal(1, (await tablet.SyncAsync(transport, Ct)).Conflicts);
        Assert.Null(tablet.View(Song).ServerValue); Assert.Equal(2, tablet.View(Song).ServerVersion);
        await tablet.ResolveAsync(Song, keepLocal: false, Ct);
        Assert.Null(tablet.View(Song).Value);
        var third = Replica("third"); await third.SyncAsync(transport, Ct);
        Assert.Null(third.View(Song).Value); Assert.Equal(2, third.Cursor);
    }
    [Fact]
    public async Task Pull_preserves_a_pending_edit_not_in_the_captured_push_batch()
    {
        var transport = new SimulatedSyncServer().ForSimulatedUser("demo-reader");
        var phone = Replica(); var tablet = Replica("tablet");
        phone.Queue(Draft, "Remote text"); await phone.SyncAsync(transport, Ct);
        var injected = new Intercept(transport) { Pull = (after, limit, ct) =>
        {
            tablet.Queue(Draft, "Typed while downloading");
            return transport.PullAsync(after, limit, ct);
        }};
        await tablet.SyncAsync(injected, Ct);
        Assert.Equal("Typed while downloading", tablet.View(Draft).Value);
        Assert.Equal("Remote text", tablet.View(Draft).ServerValue);
        Assert.True(tablet.View(Draft).Pending);
        Assert.Equal(1, (await tablet.SyncAsync(transport, Ct)).Conflicts);
    }
    [Fact]
    public async Task Another_account_is_rejected_before_transport_and_file_reuse_is_refused()
    {
        var phone = Replica(); phone.Queue(Song, "saved");
        var server = new SimulatedSyncServer(); var other = server.ForSimulatedUser("other-demo");
        await Assert.ThrowsAsync<InvalidOperationException>(() => phone.SyncAsync(other, Ct));
        Assert.Empty((await other.PullAsync(0, 100, Ct)).Rows);
        Assert.Throws<InvalidOperationException>(() => Replica(account: "other-demo"));
        Assert.True(phone.View(Song).Pending);
    }
    [Fact]
    public async Task Identical_keys_and_operation_ids_are_isolated_by_fixture_account()
    {
        var server = new SimulatedSyncServer(); var a = server.ForSimulatedUser("demo-a"); var b = server.ForSimulatedUser("demo-b");
        var id = Guid.NewGuid();
        await a.PushAsync(new(id, Draft, 0, "Account A"), Ct);
        await b.PushAsync(new(id, Draft, 0, "Account B"), Ct);
        Assert.Equal("Account A", (await a.PullAsync(0, 100, Ct)).Rows.Single().Value);
        Assert.Equal("Account B", (await b.PullAsync(0, 100, Ct)).Rows.Single().Value);
    }
    [Fact]
    public async Task Operation_id_cannot_be_reused_for_changed_payload()
    {
        var t = new SimulatedSyncServer().ForSimulatedUser("demo-reader"); var id = Guid.NewGuid();
        await t.PushAsync(new(id, Draft, 0, "First"), Ct);
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.PushAsync(new(id, Draft, 0, "Changed"), Ct));
    }
    [Fact]
    public async Task One_pass_is_bounded_and_a_later_pass_resumes_from_the_cursor()
    {
        var t = new SimulatedSyncServer().ForSimulatedUser("demo-reader");
        for (var i = 0; i < 105; i++) await t.PushAsync(new(Guid.NewGuid(), new(UserDataKind.Favorite, "item-" + i), 0, "saved"), Ct);
        var phone = Replica(); var first = await phone.SyncAsync(t, Ct);
        Assert.Equal(100, first.Downloaded); Assert.True(first.MoreRemoteChanges); Assert.Equal(100, phone.Cursor);
        var second = await phone.SyncAsync(t, Ct);
        Assert.Equal(5, second.Downloaded); Assert.False(second.MoreRemoteChanges); Assert.Equal(105, phone.Cursor);
    }
    [Fact]
    public async Task Failed_cursor_commit_rolls_back_downloaded_rows_and_reopen_retries()
    {
        var t = new SimulatedSyncServer().ForSimulatedUser("demo-reader");
        await t.PushAsync(new(Guid.NewGuid(), Song, 0, "saved"), Ct);
        var phone = Replica();
        using (var db = new SqliteConnection("Data Source=" + Path.Combine(directory, "phone.sqlite3")))
        {
            db.Open(); using var cmd = db.CreateCommand();
            cmd.CommandText = "CREATE TRIGGER fail_cursor BEFORE UPDATE OF cursor ON replica_meta BEGIN SELECT RAISE(ABORT,'fixture failure'); END;";
            cmd.ExecuteNonQuery();
        }
        await Assert.ThrowsAsync<SqliteException>(() => phone.SyncAsync(t, Ct));
        Assert.Equal(0, phone.Cursor); Assert.Null(phone.View(Song).Value);
        using (var db = new SqliteConnection("Data Source=" + Path.Combine(directory, "phone.sqlite3")))
        {
            db.Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "DROP TRIGGER fail_cursor;"; cmd.ExecuteNonQuery();
        }
        var reopened = Replica(); await reopened.SyncAsync(t, Ct);
        Assert.Equal(1, reopened.Cursor); Assert.Equal("saved", reopened.View(Song).Value);
    }
    [Fact]
    public async Task Canceled_after_server_commit_keeps_operation_for_replay()
    {
        var t = new SimulatedSyncServer().ForSimulatedUser("demo-reader"); var phone = Replica(); phone.Queue(Song, "saved");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var intercept = new Intercept(t) { Push = async (edit, ct) =>
        {
            var reply = await t.PushAsync(edit, ct); source.Cancel(); return reply;
        }};
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => phone.SyncAsync(intercept, source.Token));
        Assert.True(phone.View(Song).Pending);
        await phone.SyncAsync(t, Ct); Assert.Equal(1, phone.Cursor);
    }
    [Fact]
    public async Task A_second_worker_waits_and_does_not_send_the_same_queue_concurrently()
    {
        var t = new SimulatedSyncServer().ForSimulatedUser("demo-reader"); var phone = Replica(); phone.Queue(Song, "saved");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var calls = 0;
        var intercept = new Intercept(t) { Push = async (edit, ct) =>
        {
            Interlocked.Increment(ref calls); entered.SetResult(); await release.Task.WaitAsync(ct);
            return await t.PushAsync(edit, ct);
        }};
        var first = phone.SyncAsync(intercept, Ct); await entered.Task.WaitAsync(Ct);
        var second = phone.SyncAsync(intercept, Ct); Assert.False(second.IsCompleted);
        release.SetResult(); await Task.WhenAll(first, second); Assert.Equal(1, calls);
    }
    [Fact]
    public void Invalid_values_and_a_second_unsettled_edit_are_rejected()
    {
        var phone = Replica();
        Assert.Throws<ArgumentException>(() => phone.Queue(new(UserDataKind.Progress, "book"), "101"));
        Assert.Throws<ArgumentException>(() => phone.Queue(new((UserDataKind)999, "book"), "x"));
        phone.Queue(Song, "saved"); Assert.Throws<SqliteException>(() => phone.Queue(Song, null));
        Assert.True(phone.View(Song).Pending);
    }
    [Fact]
    public async Task Malformed_or_wrong_account_pages_do_not_advance_cursor()
    {
        var t = new SimulatedSyncServer().ForSimulatedUser("demo-reader"); var phone = Replica();
        var bad = new Intercept(t) { Pull = (after, _, _) => Task.FromResult(new ChangePage("demo-reader", after, 2,
            [new(Song, 2, "saved")], false)) };
        await Assert.ThrowsAsync<InvalidDataException>(() => phone.SyncAsync(bad, Ct));
        Assert.Equal(0, phone.Cursor); Assert.Null(phone.View(Song).Value);
        var wrong = new Intercept(t) { Pull = (after, _, _) => Task.FromResult(new ChangePage("other", after, after, [], false)) };
        await Assert.ThrowsAsync<InvalidDataException>(() => phone.SyncAsync(wrong, Ct));
        Assert.Equal(0, phone.Cursor);
    }
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
