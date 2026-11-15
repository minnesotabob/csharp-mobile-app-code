using GnazzosWorld.Core.Media;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class PlaybackTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "gnazzo-playback-" + Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(directory, "progress.db");
    private static TimeSpan Seconds(double value) => TimeSpan.FromSeconds(value);
    private static PlaybackSession New() => new(DemoAudio.Items);
    private static AudioCheckpoint Saved(long seconds = 4, bool completed = false) =>
        new("demo-chimes", "pcm-v1", Seconds(completed ? 10 : seconds).Ticks, Seconds(10).Ticks, completed);
    public PlaybackTests() => Directory.CreateDirectory(directory);
    [Fact]
    public void Queue_rejects_duplicate_id_and_excessive_item_count()
    {
        Assert.Throws<ArgumentException>(() => new PlaybackSession([DemoAudio.Items[0], DemoAudio.Items[0]]));
        Assert.Throws<ArgumentException>(() => new PlaybackSession(Array.Empty<AudioItem>()));
        Assert.Throws<ArgumentException>(() => new PlaybackSession(Enumerable.Repeat(DemoAudio.Items[0], 33)));
    }
    [Theory]
    [InlineData("https://open.spotify.com/track/example")]
    [InlineData("../private.wav")]
    [InlineData("/tmp/private.wav")]
    public void Resource_contract_does_not_accept_web_links_or_arbitrary_paths(string source)
        => Assert.Throws<ArgumentException>(() => new AudioItem("demo", "v1", "Fixture", source));
    [Fact]
    public void Unavailable_item_never_becomes_playable()
    {
        var session = New(); var ticket = session.BeginSelection(2);
        Assert.Equal(PlaybackState.Unavailable, session.State); Assert.False(session.CanPlay);
        Assert.False(session.Ready(ticket, Seconds(10), Seconds(0))); Assert.Null(session.Checkpoint());
    }
    [Fact]
    public void Open_acknowledgment_restores_observed_position_without_autoplay()
    {
        var session = New(); var ticket = session.BeginSelection(0);
        Assert.False(session.CanPlay); Assert.True(session.Ready(ticket, Seconds(10), Seconds(3.75)));
        Assert.Equal(PlaybackState.Ready, session.State); Assert.True(session.CanPlay);
        Assert.Equal(Seconds(3.75), session.Position);
    }
    [Theory]
    [InlineData(PlaybackSignal.Position)]
    [InlineData(PlaybackSignal.Ended)]
    [InlineData(PlaybackSignal.Failed)]
    public void Old_source_observations_cannot_change_the_next_selection(PlaybackSignal signal)
    {
        var session = New(); var old = session.BeginSelection(0); session.Ready(old, Seconds(10), Seconds(2));
        var current = session.BeginSelection(1); session.Ready(current, Seconds(14), Seconds(1));
        session.Observe(new(old, signal, Seconds(9)));
        Assert.Equal(PlaybackState.Ready, session.State); Assert.Equal(Seconds(1), session.Position);
        Assert.False(session.Ready(old, Seconds(10), Seconds(8)));
    }
    [Fact]
    public void A_to_b_to_a_still_requires_a_fresh_source_ticket()
    {
        var session = New(); var old = session.BeginSelection(0); session.BeginSelection(1); session.BeginSelection(0);
        Assert.False(session.IsCurrent(old)); Assert.False(session.Ready(old, Seconds(10), Seconds(4)));
    }
    [Fact]
    public void Closing_invalidates_inflight_open_and_late_end()
    {
        var session = New(); var ticket = session.BeginSelection(0); session.Close();
        Assert.False(session.Ready(ticket, Seconds(10), Seconds(3)));
        session.Observe(new(ticket, PlaybackSignal.Ended, Seconds(10)));
        Assert.Equal(PlaybackState.Empty, session.State); Assert.Null(session.Checkpoint());
        Assert.Null(session.Current);
    }
    [Fact]
    public void Partial_resume_uses_absolute_time_for_the_same_media_revision()
    {
        var session = New(); var ticket = session.BeginSelection(0);
        Assert.Equal(Seconds(4), session.ResumeTarget(ticket, Seconds(10), Saved()));
        Assert.Equal(PlaybackState.Loading, session.State); // A target is not a confirmed native seek.
    }
    [Fact]
    public void Completed_checkpoint_reopens_at_start_without_autoplay()
    {
        var session = New(); var ticket = session.BeginSelection(0);
        Assert.Equal(TimeSpan.Zero, session.ResumeTarget(ticket, Seconds(10), Saved(completed: true)));
        session.Ready(ticket, Seconds(10), TimeSpan.Zero); Assert.Equal(PlaybackState.Ready, session.State);
    }
    [Fact]
    public void Different_revision_or_another_item_does_not_inherit_saved_position()
    {
        var session = New(); var ticket = session.BeginSelection(0);
        Assert.Equal(TimeSpan.Zero, session.ResumeTarget(ticket, Seconds(10), Saved() with { Revision = "pcm-v2" }));
        Assert.Equal(TimeSpan.Zero, session.ResumeTarget(ticket, Seconds(10), Saved() with { ItemId = "demo-pulses" }));
    }
    [Fact]
    public void Checkpoint_beyond_new_duration_restarts_instead_of_seeking_outside_media()
    {
        var session = New(); var ticket = session.BeginSelection(0);
        Assert.Equal(TimeSpan.Zero, session.ResumeTarget(ticket, Seconds(3), Saved()));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(86401)]
    public void Unknown_or_unsupported_live_duration_does_not_enable_controls(int seconds)
    {
        var session = New(); var ticket = session.BeginSelection(0);
        Assert.Throws<InvalidDataException>(() => session.Ready(ticket, Seconds(seconds), TimeSpan.Zero));
        Assert.False(session.CanPlay);
    }
    [Fact]
    public void Seek_target_is_clamped_but_position_changes_only_from_observation()
    {
        var session = New(); var ticket = session.BeginSelection(0); session.Ready(ticket, Seconds(10), Seconds(2));
        Assert.Equal(TimeSpan.Zero, session.SeekTarget(Seconds(-5))); Assert.Equal(Seconds(10), session.SeekTarget(Seconds(30)));
        Assert.Equal(Seconds(2), session.Position);
        session.Observe(new(ticket, PlaybackSignal.Position, Seconds(7.8))); Assert.Equal(Seconds(7.8), session.Position);
    }
    [Fact]
    public void Buffering_does_not_claim_a_position_advance_or_ended_media()
    {
        var session = New(); var ticket = session.BeginSelection(0); session.Ready(ticket, Seconds(10), Seconds(2));
        session.Observe(new(ticket, PlaybackSignal.Playing, Seconds(2)));
        session.Observe(new(ticket, PlaybackSignal.Buffering, Seconds(9)));
        Assert.Equal(Seconds(2), session.Position); Assert.Equal(PlaybackState.Buffering, session.State);
        Assert.False(session.Checkpoint()!.Completed);
    }
    [Fact]
    public void Failed_source_cannot_be_revived_by_a_queued_playing_event()
    {
        var session = New(); var ticket = session.BeginSelection(0); session.Ready(ticket, Seconds(10), Seconds(2));
        session.Observe(new(ticket, PlaybackSignal.Failed, Seconds(2)));
        session.Observe(new(ticket, PlaybackSignal.Playing, Seconds(3)));
        Assert.Equal(PlaybackState.Failed, session.State); Assert.False(session.CanPlay);
        Assert.Equal(Seconds(2).Ticks, session.Checkpoint()!.PositionTicks);
    }
    [Fact]
    public void Ended_checkpoint_is_not_regressed_by_delayed_position_or_pause()
    {
        var session = New(); var ticket = session.BeginSelection(0); session.Ready(ticket, Seconds(10), Seconds(2));
        session.Observe(new(ticket, PlaybackSignal.Ended, Seconds(10)));
        session.Observe(new(ticket, PlaybackSignal.Position, Seconds(9)));
        session.Observe(new(ticket, PlaybackSignal.Paused, Seconds(9)));
        session.Observe(new(ticket, PlaybackSignal.Playing, Seconds(9)));
        Assert.Equal(PlaybackState.Ended, session.State); Assert.True(session.Checkpoint()!.Completed);
        Assert.Equal(Seconds(10), session.Position);
    }
    [Fact]
    public void Explicit_replay_can_begin_a_new_observed_pass_after_completion()
    {
        var session = New(); var ticket = session.BeginSelection(0); session.Ready(ticket, Seconds(10), Seconds(0));
        session.Observe(new(ticket, PlaybackSignal.Ended, Seconds(10))); Assert.True(session.CanPlay);
        var replay = session.BeginSelection(0); session.Ready(replay, Seconds(10), TimeSpan.Zero);
        session.Observe(new(replay, PlaybackSignal.Playing, Seconds(0)));
        session.Observe(new(replay, PlaybackSignal.Position, Seconds(0.3)));
        Assert.False(session.Checkpoint()!.Completed); Assert.Equal(Seconds(0.3), session.Position);
    }
    [Fact]
    public void Checkpoint_survives_reopen_and_is_separate_for_each_item_and_revision()
    {
        using (var database = new AudioProgressDatabase(Database)) database.Save(Saved());
        using var reopened = new AudioProgressDatabase(Database);
        Assert.Equal(Seconds(4).Ticks, reopened.Read(DemoAudio.Items[0])!.PositionTicks);
        Assert.Null(reopened.Read(DemoAudio.Items[1]));
        Assert.Null(reopened.Read(new("demo-chimes", "pcm-v2", "Replacement", "demo_chimes.wav")));
    }
    [Theory]
    [InlineData(-1, 10, false)]
    [InlineData(11, 10, false)]
    [InlineData(5, 10, true)]
    public void Invalid_checkpoint_write_preserves_last_confirmed_value(int position, int duration, bool completed)
    {
        using var db = new AudioProgressDatabase(Database); db.Save(Saved());
        var bad = new AudioCheckpoint("demo-chimes", "pcm-v1", Seconds(position).Ticks, Seconds(duration).Ticks, completed);
        Assert.Throws<InvalidDataException>(() => db.Save(bad));
        Assert.Equal(Seconds(4).Ticks, db.Read(DemoAudio.Items[0])!.PositionTicks);
    }
    [Fact]
    public void Corrupt_stored_checkpoint_is_not_reported_as_a_valid_resume()
    {
        using var db = new AudioProgressDatabase(Database); db.Save(Saved());
        using var inspection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database, Pooling = false }.ToString());
        inspection.Open(); using var command = inspection.CreateCommand(); command.CommandText = "UPDATE audio_progress SET position_ticks=-1";
        command.ExecuteNonQuery(); Assert.Throws<InvalidDataException>(() => db.Read(DemoAudio.Items[0]));
    }
    [Fact]
    public void Forgetting_one_item_does_not_remove_another_items_progress()
    {
        using var db = new AudioProgressDatabase(Database); db.Save(Saved());
        db.Save(Saved() with { ItemId = "demo-pulses" }); db.Remove(DemoAudio.Items[0]);
        Assert.Null(db.Read(DemoAudio.Items[0])); Assert.NotNull(db.Read(DemoAudio.Items[1]));
    }
    [Fact]
    public void Failed_open_cannot_be_overwritten_by_a_delayed_ready_acknowledgment()
    {
        var session = New(); var ticket = session.BeginSelection(0);
        session.Observe(new(ticket, PlaybackSignal.Failed, TimeSpan.Zero));
        Assert.False(session.Ready(ticket, Seconds(10), Seconds(1))); Assert.False(session.CanPlay);
    }
    [Fact]
    public void Rewinding_can_save_a_lower_position_instead_of_preserving_an_unwanted_maximum()
    {
        using var db = new AudioProgressDatabase(Database); db.Save(Saved(7)); db.Save(Saved(2));
        Assert.Equal(Seconds(2).Ticks, db.Read(DemoAudio.Items[0])!.PositionTicks);
    }
    [Fact]
    public void Real_storage_failure_preserves_last_save_and_retry_can_commit_the_next_position()
    {
        using var db = new AudioProgressDatabase(Database); db.Save(Saved());
        using var inspection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database, Pooling = false }.ToString());
        inspection.Open(); using var command = inspection.CreateCommand();
        command.CommandText = "CREATE TRIGGER fail_save BEFORE UPDATE ON audio_progress BEGIN SELECT RAISE(ABORT,'fixture failure'); END;";
        command.ExecuteNonQuery(); Assert.Throws<SqliteException>(() => db.Save(Saved(5)));
        Assert.Equal(Seconds(4).Ticks, db.Read(DemoAudio.Items[0])!.PositionTicks);
        command.CommandText = "DROP TRIGGER fail_save"; command.ExecuteNonQuery(); db.Save(Saved(5));
        Assert.Equal(Seconds(5).Ticks, db.Read(DemoAudio.Items[0])!.PositionTicks);
    }
    [Theory]
    [InlineData("demo_chimes.wav", 10)]
    [InlineData("demo_pulses.wav", 14)]
    [InlineData("background_timing.wav", 60)]
    public void Distributed_fixtures_match_recorded_pcm_format_duration_and_hash(string filename, int seconds)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src/GnazzosWorld.App/Resources/Raw", filename);
        var bytes = File.ReadAllBytes(path); using var reader = new BinaryReader(new MemoryStream(bytes));
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4))); reader.ReadInt32();
        Assert.Equal("WAVEfmt ", System.Text.Encoding.ASCII.GetString(reader.ReadBytes(8)));
        Assert.Equal(16, reader.ReadInt32()); Assert.Equal(1, reader.ReadInt16()); Assert.Equal(1, reader.ReadInt16());
        Assert.Equal(8000, reader.ReadInt32()); reader.ReadInt32(); reader.ReadInt16(); Assert.Equal(16, reader.ReadInt16());
        Assert.Equal("data", System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4))); Assert.Equal(seconds * 8000 * 2, reader.ReadInt32());
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "samples/audio-fixtures.json")));
        var row = Assert.Single(manifest.RootElement.GetProperty("files").EnumerateArray(), x => x.GetProperty("file").GetString() == filename);
        Assert.Equal(row.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }
    public void Dispose() => Directory.Delete(directory, recursive: true);
}
