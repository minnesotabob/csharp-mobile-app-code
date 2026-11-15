using System.Text.Json;
using GnazzosWorld.Core.Identity;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class AccountWorkspaceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "gnazzo-account-tests-" + Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(directory, "workspace.db");
    private static readonly AccountOwner A = new(new("https://fixture.auth0.com/"), "reader-a");
    private static readonly AccountOwner B = new(new("https://fixture.auth0.com/"), "reader-b");
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private readonly Clock clock = new();
    public AccountWorkspaceTests() => Directory.CreateDirectory(directory);
    private AccountWorkspace Open() => new(Database, clock);
    private static ProfilePreferences Profile(string name = "Reader A", bool sync = false) => new(name, sync, false);

    [Fact]
    public void New_account_starts_with_optional_processing_off()
    {
        using var db = Open(); var lease = db.Activate(A); var profile = db.ReadProfile(lease);
        Assert.False(profile.Preferences.SyncEnabled); Assert.False(profile.Preferences.OrderNotifications);
        Assert.Null(profile.ConsentUtc); Assert.False(db.CanSynchronize(lease));
    }
    [Fact]
    public void Preferences_survive_restart_only_in_their_own_partition()
    {
        using (var db = Open()) db.SaveProfile(db.Activate(A), Profile("Anniversary reader"), 0);
        using var reopened = Open(); var b = reopened.Activate(B);
        Assert.Equal("", reopened.ReadProfile(b).Preferences.DisplayName);
        Assert.Equal("Anniversary reader", reopened.ReadProfile(reopened.Activate(A)).Preferences.DisplayName);
    }
    [Fact]
    public void Identical_subject_at_different_issuer_is_a_different_account()
    {
        using var db = Open(); db.SaveProfile(db.Activate(A), Profile(), 0);
        var other = new AccountOwner(new("https://different.auth0.com/"), A.Subject);
        Assert.Equal(0, db.ReadProfile(db.Activate(other)).Revision);
    }
    [Fact]
    public void Switching_accounts_invalidates_old_edits_exports_and_receipts()
    {
        using var db = Open(); var a = db.Activate(A); var op = db.QueueRequest(a, LifecycleKind.Deletion);
        var b = db.Activate(B); Assert.False(db.IsCurrent(a)); Assert.True(db.IsCurrent(b));
        Assert.Throws<InvalidOperationException>(() => db.SaveProfile(a, Profile(), 0));
        Assert.Throws<InvalidOperationException>(() => db.ExportLocalProfile(a));
        Assert.Throws<InvalidOperationException>(() => db.ApplyReceipt(a, op.Id, op.Kind, LifecycleState.Completed));
        Assert.Equal(0, db.ReadProfile(b).Revision);
    }
    [Fact]
    public void Returning_to_same_account_does_not_revalidate_an_old_lease()
    {
        using var db = Open(); var old = db.Activate(A); db.Activate(B); var fresh = db.Activate(A);
        Assert.False(db.IsCurrent(old)); Assert.True(db.IsCurrent(fresh));
        Assert.Throws<InvalidOperationException>(() => db.ReadProfile(old));
    }
    [Fact]
    public void Handle_from_another_coordinator_is_rejected()
    {
        using var first = Open(); var foreign = first.Activate(A);
        using var second = Open(); second.Activate(A);
        Assert.Throws<InvalidOperationException>(() => second.ReadProfile(foreign));
    }
    [Fact]
    public void Detach_removes_access_without_claiming_data_deletion()
    {
        using var db = Open(); var lease = db.Activate(A); db.SaveProfile(lease, Profile(), 0); db.Detach();
        Assert.False(db.IsCurrent(lease)); Assert.False(db.IsCurrent(null!));
        Assert.Throws<InvalidOperationException>(() => db.ReadProfile(lease));
        Assert.Equal("Reader A", db.ReadProfile(db.Activate(A)).Preferences.DisplayName);
    }
    [Fact]
    public void Consent_is_recorded_on_enable_and_not_rewritten_by_name_edit()
    {
        using var db = Open(); var lease = db.Activate(A);
        var enabled = db.SaveProfile(lease, Profile(sync: true), 0); Assert.True(db.CanSynchronize(lease));
        Assert.Equal(AccountWorkspace.NoticeVersion, enabled.ConsentNotice); Assert.Equal(clock.Now, enabled.ConsentUtc);
        clock.Now = clock.Now.AddDays(1);
        var edited = db.SaveProfile(lease, Profile("New name", true), enabled.Revision);
        Assert.Equal(enabled.ConsentUtc, edited.ConsentUtc);
    }
    [Fact]
    public void Withdrawal_stops_new_sync_and_reenable_records_new_choice()
    {
        using var db = Open(); var lease = db.Activate(A);
        var one = db.SaveProfile(lease, Profile(sync: true), 0);
        var off = db.SaveProfile(lease, Profile(), one.Revision);
        Assert.False(db.CanSynchronize(lease)); Assert.Null(off.ConsentNotice); Assert.Null(off.ConsentUtc);
        clock.Now = clock.Now.AddDays(1);
        Assert.Equal(clock.Now, db.SaveProfile(lease, Profile(sync: true), off.Revision).ConsentUtc);
    }
    [Fact]
    public void Old_editor_revision_cannot_overwrite_newer_privacy_choice()
    {
        using var db = Open(); var lease = db.Activate(A); db.SaveProfile(lease, Profile(sync: true), 0);
        Assert.Throws<InvalidOperationException>(() => db.SaveProfile(lease, Profile(), 0));
        Assert.True(db.CanSynchronize(lease));
    }
    [Theory]
    [InlineData("http://fixture.example/")]
    [InlineData("https://user:secret@fixture.example/")]
    [InlineData("https://fixture.example/?token=secret")]
    [InlineData("https://fixture.example/#fragment")]
    public void Invalid_partition_issuer_is_rejected(string issuer)
        => Assert.Throws<ArgumentException>(() => new AccountOwner(new(issuer), "reader"));
    [Fact]
    public void Invalid_display_name_does_not_change_existing_profile()
    {
        using var db = Open(); var lease = db.Activate(A); db.SaveProfile(lease, Profile(), 0);
        Assert.Throws<ArgumentException>(() => db.SaveProfile(lease, Profile("name\nlog injection"), 1));
        Assert.Equal("Reader A", db.ReadProfile(lease).Preferences.DisplayName);
    }
    [Fact]
    public void Local_export_is_scoped_and_excludes_another_accounts_profile()
    {
        using var db = Open(); db.SaveProfile(db.Activate(B), Profile("Private B"), 0);
        var a = db.Activate(A); db.SaveProfile(a, Profile("Public alias A"), 0);
        var text = db.ExportLocalProfile(a); using var json = JsonDocument.Parse(text);
        Assert.Equal("chapter25-local-profile", json.RootElement.GetProperty("scope").GetString());
        Assert.Contains("Public alias A", text); Assert.DoesNotContain("Private B", text);
        Assert.DoesNotContain(A.Subject, text); Assert.DoesNotContain(A.Issuer, text);
    }
    [Fact]
    public void Retry_after_lost_reply_and_restart_reuses_durable_operation_id()
    {
        Guid id;
        using (var db = Open()) id = db.QueueRequest(db.Activate(A), LifecycleKind.Deletion).Id;
        using var reopened = Open(); var a = reopened.Activate(A);
        var retry = reopened.QueueRequest(a, LifecycleKind.Deletion);
        Assert.Equal(id, retry.Id); Assert.Equal(LifecycleState.Queued, retry.State);
    }
    [Fact]
    public void Pending_deletion_freezes_writes_export_and_new_sync_before_a_reply()
    {
        using var db = Open(); var a = db.Activate(A); db.SaveProfile(a, Profile(sync: true), 0);
        db.QueueRequest(a, LifecycleKind.Deletion);
        Assert.False(db.CanSynchronize(a));
        Assert.Throws<InvalidOperationException>(() => db.SaveProfile(a, Profile(), 1));
        Assert.Throws<InvalidOperationException>(() => db.ExportLocalProfile(a));
        Assert.Throws<InvalidOperationException>(() => db.QueueRequest(a, LifecycleKind.Export));
    }
    [Fact]
    public void Reauthentication_and_acceptance_do_not_claim_completed_removal()
    {
        using var db = Open(); var a = db.Activate(A); db.SaveProfile(a, Profile(), 0);
        var op = db.QueueRequest(a, LifecycleKind.Deletion);
        db.ApplyReceipt(a, op.Id, op.Kind, LifecycleState.ReauthenticationRequired);
        Assert.Equal("Reader A", db.ReadProfile(a).Preferences.DisplayName);
        db.ApplyReceipt(a, op.Id, op.Kind, LifecycleState.Accepted);
        Assert.Equal("Reader A", db.ReadProfile(a).Preferences.DisplayName);
    }
    [Fact]
    public void Another_account_cannot_apply_a_foreign_receipt_even_with_its_operation_id()
    {
        using var db = Open(); var opA = db.QueueRequest(db.Activate(A), LifecycleKind.Deletion);
        var b = db.Activate(B); db.QueueRequest(b, LifecycleKind.Deletion);
        Assert.Throws<InvalidOperationException>(() => db.ApplyReceipt(b, opA.Id, opA.Kind, LifecycleState.Completed));
        Assert.Equal(LifecycleState.Queued, db.GetOperation(b, LifecycleKind.Deletion)!.State);
    }
    [Fact]
    public void Completed_local_removal_purges_only_that_profile_and_export_job()
    {
        using var db = Open(); db.SaveProfile(db.Activate(B), Profile("Keep B"), 0);
        var a = db.Activate(A); db.SaveProfile(a, Profile(), 0); db.QueueRequest(a, LifecycleKind.Export);
        var op = db.QueueRequest(a, LifecycleKind.Deletion);
        db.ApplyReceipt(a, op.Id, op.Kind, LifecycleState.Completed);
        Assert.Null(db.GetOperation(a, LifecycleKind.Export)); Assert.False(db.CanSynchronize(a));
        Assert.Throws<InvalidOperationException>(() => db.ReadProfile(a));
        Assert.Equal("Keep B", db.ReadProfile(db.Activate(B)).Preferences.DisplayName);
    }
    [Fact]
    public void Receipt_completion_survives_restart_and_cannot_recreate_removed_profile()
    {
        Guid id;
        using (var db = Open())
        {
            var a = db.Activate(A); var op = db.QueueRequest(a, LifecycleKind.Deletion); id = op.Id;
            db.ApplyReceipt(a, op.Id, op.Kind, LifecycleState.Completed);
        }
        using var reopened = Open(); var lease = reopened.Activate(A);
        var receipt = reopened.QueueRequest(lease, LifecycleKind.Deletion);
        Assert.Equal(id, receipt.Id); Assert.Equal(LifecycleState.Completed, receipt.State);
        Assert.Throws<InvalidOperationException>(() => reopened.SaveProfile(lease, Profile(), 0));
    }
    [Fact]
    public void Out_of_order_or_duplicate_receipts_do_not_regress_or_resurrect_state()
    {
        using var db = Open(); var a = db.Activate(A); var op = db.QueueRequest(a, LifecycleKind.Deletion);
        db.ApplyReceipt(a, op.Id, op.Kind, LifecycleState.Completed);
        Assert.Equal(LifecycleState.Completed, db.ApplyReceipt(a, op.Id, op.Kind, LifecycleState.Accepted).State);
        Assert.Equal(LifecycleState.Completed, db.ApplyReceipt(a, op.Id, op.Kind, LifecycleState.Completed).State);
        Assert.Throws<InvalidOperationException>(() => db.ReadProfile(a));
    }
    [Fact]
    public void Failed_local_purge_rolls_back_receipt_and_can_retry_same_operation()
    {
        using var db = Open(); var a = db.Activate(A); db.SaveProfile(a, Profile(), 0);
        db.QueueRequest(a, LifecycleKind.Export); var op = db.QueueRequest(a, LifecycleKind.Deletion);
        db.ApplyReceipt(a, op.Id, op.Kind, LifecycleState.Accepted);
        using var inspection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database, Pooling = false }.ToString());
        inspection.Open(); using var command = inspection.CreateCommand();
        command.CommandText = "CREATE TRIGGER fail_purge BEFORE DELETE ON workspace_profile BEGIN SELECT RAISE(ABORT,'fixture failure'); END;";
        command.ExecuteNonQuery();
        Assert.Throws<SqliteException>(() => db.ApplyReceipt(a, op.Id, op.Kind, LifecycleState.Completed));
        Assert.Equal("Reader A", db.ReadProfile(a).Preferences.DisplayName);
        Assert.NotNull(db.GetOperation(a, LifecycleKind.Export));
        Assert.Equal(LifecycleState.Accepted, db.GetOperation(a, op.Kind)!.State);
        command.CommandText = "DROP TRIGGER fail_purge"; command.ExecuteNonQuery();
        Assert.Equal(LifecycleState.Completed, db.ApplyReceipt(a, op.Id, op.Kind, LifecycleState.Completed).State);
    }
    public void Dispose() => Directory.Delete(directory, recursive: true);
}
