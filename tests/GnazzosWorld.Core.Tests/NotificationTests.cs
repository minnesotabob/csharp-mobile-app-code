using GnazzosWorld.Core.Notifications;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class NotificationTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly string folder = Path.Combine(Path.GetTempPath(), "gnazzo-links-" + Guid.NewGuid().ToString("N"));
    private string PathName { get { Directory.CreateDirectory(folder); return Path.Combine(folder, "inbox.db"); } }
    private static NotificationEnvelope Envelope(string id = "public-item", LinkKind kind = LinkKind.Catalog) => NotificationEnvelope.Local(new(kind, id), Now);
    [Theory]
    [InlineData("gnazzosworld://open/catalog/song-1", LinkKind.Catalog)]
    [InlineData("gnazzosworld://open/request/anniversary-demo", LinkKind.Request)]
    public void Canonical_links_produce_typed_target(string raw, LinkKind expected)
    { var target = LinkTarget.Parse(raw); Assert.NotNull(target); Assert.Equal(expected, target.Kind); Assert.Equal(raw, target.Canonical); }
    [Theory]
    [InlineData("https://evil.example/open/catalog/a")]
    [InlineData("gnazzosworld://other/catalog/a")]
    [InlineData("gnazzosworld://open/catalog/../secret")]
    [InlineData("gnazzosworld://open/catalog/%61")]
    [InlineData("gnazzosworld://open/catalog/a?account=b")]
    [InlineData("gnazzosworld://open/catalog/a#x")]
    [InlineData("gnazzosworld://user@open/catalog/a")]
    [InlineData("gnazzosworld://open//catalog/a")]
    [InlineData("gnazzosworld://open/catalog/a/")]
    [InlineData("gnazzosworld://open/settings/delete")]
    [InlineData("GNazzosworld://open/catalog/a")]
    public void Noncanonical_or_unapproved_links_are_rejected(string raw) => Assert.Null(LinkTarget.Parse(raw));
    [Fact]
    public void Share_builder_only_builds_catalog_destination() => Assert.Equal("gnazzosworld://open/catalog/song-1", LinkTarget.PublicShare("song-1"));
    [Fact]
    public void Oversized_identifier_is_rejected() => Assert.Null(LinkTarget.Parse("gnazzosworld://open/catalog/" + new string('a', 65)));
    [Fact]
    public void Encoded_envelope_round_trips_only_within_bounded_expiry()
    { var e = Envelope(); Assert.Equal(e, NotificationEnvelope.Decode(e.Encode(), Now)); Assert.Null(NotificationEnvelope.Decode(e.Encode(), Now.AddMinutes(11))); }
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Unsupported_envelope_schema_is_rejected(int schema)
    { var e = Envelope() with { Schema = schema }; Assert.Null(NotificationEnvelope.Decode(e.Encode(), Now)); }
    [Fact]
    public void Excessive_expiry_is_rejected() => Assert.False((Envelope() with { ExpiresUnix = Now.AddHours(1).ToUnixTimeSeconds() }).IsValid(Now));
    [Fact]
    public void Payload_cannot_supply_unrecognized_customer_fields()
    { var text = Envelope().Encode(); Assert.Null(NotificationEnvelope.Decode(text[..^1] + ",\"customerName\":\"private\"}", Now)); }
    [Fact]
    public void Duplicate_json_members_are_rejected()
    { var text = Envelope().Encode(); Assert.Null(NotificationEnvelope.Decode("{\"schema\":1," + text[1..], Now)); }
    [Fact]
    public void Invalid_json_and_oversized_payloads_are_rejected()
    { Assert.Null(NotificationEnvelope.Decode("{", Now)); Assert.Null(NotificationEnvelope.Decode(new string('x', 1025), Now)); }
    [Fact]
    public void Cold_start_hint_survives_connection_reopen_without_navigation()
    {
        var item = Envelope(); using (var db = new RouteInbox(PathName)) Assert.True(db.Accept(item, Now));
        using var reopened = new RouteInbox(PathName); Assert.Equal(item.MessageId, reopened.Peek(Now)!.MessageId);
    }
    [Fact]
    public void Completed_receipt_deduplicates_notification_after_process_restart()
    {
        var item = Envelope(); using (var db = new RouteInbox(PathName)) { db.Accept(item, Now); db.Complete(item.MessageId); }
        using var reopened = new RouteInbox(PathName); Assert.False(reopened.Accept(item, Now)); Assert.Null(reopened.Peek(Now));
    }
    [Fact]
    public void Pending_same_target_coalesces_different_delivery_ids()
    { using var db = new RouteInbox(PathName); Assert.True(db.Accept(Envelope(), Now)); Assert.False(db.Accept(Envelope(), Now)); }
    [Fact]
    public void Navigation_failure_leaves_hint_pending_until_explicit_completion()
    { using var db = new RouteInbox(PathName); var e = Envelope(); db.Accept(e, Now); Assert.Equal(db.Peek(Now), db.Peek(Now)); db.Complete(e.MessageId); Assert.Null(db.Peek(Now)); }
    [Fact]
    public void Expired_hints_are_removed_before_selection()
    { using var db = new RouteInbox(PathName); db.Accept(Envelope(), Now); Assert.Null(db.Peek(Now.AddMinutes(11))); }
    [Fact]
    public void Inbox_bounds_pending_intents_to_eight()
    { using var db = new RouteInbox(PathName); for (var i = 0; i < 8; i++) Assert.True(db.Accept(Envelope("item-" + i), Now)); Assert.False(db.Accept(Envelope("overflow"), Now)); }
    [Fact]
    public void Receipt_retention_is_bounded_and_expires()
    {
        using var db = new RouteInbox(PathName);
        for (var i = 0; i < 128; i++) { var e = Envelope("item-" + i); Assert.True(db.Accept(e, Now)); db.Complete(e.MessageId); }
        Assert.False(db.Accept(Envelope("overflow"), Now));
        Assert.True(db.Accept(NotificationEnvelope.Local(new(LinkKind.Catalog, "fresh"), Now.AddMinutes(11)), Now.AddMinutes(11)));
    }
    [Fact]
    public void Clearing_hints_removes_pending_private_request_pointer()
    { using var db = new RouteInbox(PathName); db.Accept(Envelope("anniversary-demo", LinkKind.Request), Now); db.Clear(); Assert.Null(db.Peek(Now)); }
    [Fact]
    public async Task Permission_grant_registers_after_persisting_generation_and_rotation_advances_it()
    {
        var sink = new Sink(); long stored = 0; var coordinator = new PushRegistrationCoordinator(sink, Guid.NewGuid().ToString("N"), stored, n => stored = n);
        sink.BeforeSave = b => Assert.Equal(b.Generation, stored);
        await coordinator.UpdateAsync(PushPermission.Granted, "fcm", "synthetic-token-1", Ct);
        await coordinator.UpdateAsync(PushPermission.Granted, "fcm", "synthetic-token-2", Ct);
        Assert.True(coordinator.RegistrationConfirmed); Assert.Equal(2, stored); Assert.Equal("synthetic-token-2", sink.Binding!.Token);
    }
    [Theory]
    [InlineData(PushPermission.Unknown)]
    [InlineData(PushPermission.Denied)]
    public async Task Missing_permission_requests_unregistration_without_upsert(PushPermission permission)
    {
        var sink = new Sink(); var c = new PushRegistrationCoordinator(sink, Guid.NewGuid().ToString("N"), 0, _ => { });
        await c.UpdateAsync(permission, "fcm", "synthetic", Ct); Assert.Equal(1, sink.Removes); Assert.Null(sink.Binding); Assert.False(c.RegistrationConfirmed);
    }
    [Fact]
    public async Task Lost_registration_reply_is_unconfirmed_and_next_attempt_has_new_generation()
    {
        var sink = new Sink { Fail = true }; long stored = 0; var id = Guid.NewGuid().ToString("N");
        var first = new PushRegistrationCoordinator(sink, id, 0, n => stored = n);
        await Assert.ThrowsAsync<IOException>(() => first.UpdateAsync(PushPermission.Granted, "apns", "synthetic", Ct)); Assert.False(first.RegistrationConfirmed);
        sink.Fail = false; var afterRestart = new PushRegistrationCoordinator(sink, id, stored, n => stored = n);
        await afterRestart.UpdateAsync(PushPermission.Granted, "apns", "synthetic", Ct); Assert.Equal(2, sink.Binding!.Generation);
    }
    [Fact]
    public async Task Failed_generation_persistence_prevents_token_send()
    {
        var sink = new Sink(); var c = new PushRegistrationCoordinator(sink, Guid.NewGuid().ToString("N"), 0, _ => throw new IOException());
        await Assert.ThrowsAsync<IOException>(() => c.UpdateAsync(PushPermission.Granted, "fcm", "synthetic", Ct)); Assert.Null(sink.Binding);
    }
    [Fact]
    public async Task Unregistration_failure_cannot_be_reported_as_confirmed_registration()
    {
        var sink = new Sink(); var c = new PushRegistrationCoordinator(sink, Guid.NewGuid().ToString("N"), 0, _ => { });
        await c.UpdateAsync(PushPermission.Granted, "fcm", "synthetic", Ct); sink.Fail = true;
        await Assert.ThrowsAsync<IOException>(() => c.DisableAsync(Ct)); Assert.False(c.RegistrationConfirmed);
    }
    [Theory]
    [InlineData("unsupported", "token")]
    [InlineData("fcm", "has space")]
    [InlineData("apns", "")]
    public void Invalid_provider_or_token_is_rejected(string provider, string token) => Assert.False(new PushBinding(Guid.NewGuid().ToString("N"), provider, token, 1).IsValid());
    [Fact]
    public void Device_token_is_redacted_from_record_text() => Assert.DoesNotContain("secret-token", new PushBinding(Guid.NewGuid().ToString("N"), "fcm", "secret-token", 1).ToString());
    private sealed class Sink : IPushRegistrationSink
    {
        public PushBinding? Binding; public int Removes; public bool Fail; public Action<PushBinding>? BeforeSave;
        public Task UpsertAsync(PushBinding binding, CancellationToken ct)
        { BeforeSave?.Invoke(binding); if (Fail) throw new IOException(); Binding = binding; return Task.CompletedTask; }
        public Task RemoveAsync(string installationId, CancellationToken ct)
        { Removes++; if (Fail) throw new IOException(); Binding = null; return Task.CompletedTask; }
    }
    public void Dispose() { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
}
