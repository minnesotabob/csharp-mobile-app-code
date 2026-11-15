using GnazzosWorld.Core.Media;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class BackgroundPlaybackTests
{
    private readonly Player player = new();
    private readonly Store store = new();
    private BackgroundPlaybackOwner New() => new(player, store, BackgroundDemoAudio.Items);
    [Fact]
    public async Task Loaded_source_is_ready_without_autoplay()
    {
        using var owner = New(); await owner.LoadAsync(0);
        Assert.Equal(PlaybackState.Ready, owner.Session.State); Assert.Equal(0, player.Plays);
    }
    [Fact]
    public async Task Background_requires_opt_in_and_return_does_not_reload()
    {
        using var owner = New(); await owner.LoadAsync(0); owner.Play(); owner.SetForeground(false);
        Assert.Equal(PlaybackState.Empty, owner.Session.State);
        owner.SetForeground(true); Assert.Equal(1, player.Opens); Assert.Equal(1, player.Plays);
    }
    [Fact]
    public async Task Allowed_current_source_survives_window_stop_and_resume_without_commands()
    {
        using var owner = New(); await owner.LoadAsync(0); owner.SetBackgroundAllowed(true); owner.Play();
        var stops = player.Stops; player.Position = TimeSpan.FromSeconds(12); owner.SetForeground(false);
        Assert.Equal(PlaybackState.Playing, owner.Session.State); Assert.Equal(stops, player.Stops);
        Assert.Equal(TimeSpan.FromSeconds(12).Ticks, store.Saved!.PositionTicks);
        owner.SetForeground(true); Assert.Equal(1, player.Plays); Assert.Equal(1, player.Opens);
    }
    [Fact]
    public async Task New_source_is_rejected_even_when_background_continuation_allowed()
    {
        using var owner = New(); owner.SetBackgroundAllowed(true); owner.SetForeground(false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.LoadAsync(0)); Assert.Equal(0, player.Opens);
    }
    [Fact]
    public async Task Revoking_background_permission_releases_current_source()
    {
        using var owner = New(); await owner.LoadAsync(0); owner.SetBackgroundAllowed(true); owner.Play();
        owner.SetForeground(false); owner.SetBackgroundAllowed(false);
        Assert.Equal(PlaybackState.Empty, owner.Session.State); Assert.False(owner.Session.CanPlay);
    }
    [Fact]
    public async Task Window_stop_cancels_pending_load_even_with_opt_in()
    {
        using var owner = New(); player.Delayed = true; owner.SetBackgroundAllowed(true);
        var pending = owner.LoadAsync(0); owner.SetForeground(false);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.False(owner.Busy); Assert.Equal(PlaybackState.Empty, owner.Session.State); Assert.Equal(0, player.Plays);
    }
    [Fact]
    public async Task Cancellation_ignoring_decoder_cannot_publish_ready_after_stop()
    {
        using var owner = New(); player.Delayed = true; player.IgnoreCancellation = true;
        var pending = owner.LoadAsync(0); owner.Stop(); player.Complete(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(PlaybackState.Empty, owner.Session.State);
    }
    [Fact]
    public async Task Late_old_system_control_observation_cannot_change_new_source()
    {
        using var owner = New(); await owner.LoadAsync(0); var old = player.Ticket;
        await owner.LoadAsync(0); player.Emit(old, PlaybackSignal.Playing, 19);
        Assert.Equal(PlaybackState.Ready, owner.Session.State); Assert.Equal(TimeSpan.Zero, owner.Session.Position);
    }
    [Fact]
    public async Task Remote_pause_saves_observed_position_without_app_pause_command()
    {
        using var owner = New(); await owner.LoadAsync(0); player.Emit(player.Ticket, PlaybackSignal.Paused, 17);
        Assert.Equal(PlaybackState.Paused, owner.Session.State); Assert.Equal(TimeSpan.FromSeconds(17).Ticks, store.Saved!.PositionTicks);
        Assert.Equal(0, player.Pauses);
    }
    [Fact]
    public async Task App_pause_never_automatically_resumes()
    {
        using var owner = New(); await owner.LoadAsync(0); owner.Play(); owner.Pause();
        owner.SetForeground(false); owner.SetForeground(true); Assert.Equal(1, player.Plays); Assert.Equal(1, player.Pauses);
    }
    [Theory]
    [InlineData(PlaybackSignal.Ended, true)]
    [InlineData(PlaybackSignal.Failed, false)]
    public async Task Terminal_native_event_saves_then_releases_remote_session(PlaybackSignal signal, bool completed)
    {
        using var owner = New(); await owner.LoadAsync(0); var old = player.Ticket;
        player.Emit(old, signal, 22); Assert.Equal(PlaybackState.Empty, owner.Session.State);
        Assert.Equal(completed, store.Saved!.Completed); Assert.True(player.Stops >= 2);
        player.Emit(old, PlaybackSignal.Playing, 25); Assert.Equal(PlaybackState.Empty, owner.Session.State);
    }
    [Fact]
    public async Task Save_failure_retains_previous_confirmed_checkpoint_and_reports_uncertainty()
    {
        using var owner = New(); await owner.LoadAsync(0); player.Position = TimeSpan.FromSeconds(4); owner.Checkpoint();
        store.Fail = true; player.Position = TimeSpan.FromSeconds(9); owner.Pause();
        Assert.True(owner.SaveUnconfirmed); Assert.Equal(TimeSpan.FromSeconds(4).Ticks, store.Saved!.PositionTicks);
        store.Fail = false; owner.Checkpoint(); Assert.False(owner.SaveUnconfirmed);
        Assert.Equal(TimeSpan.FromSeconds(9).Ticks, store.Saved!.PositionTicks);
    }
    [Fact]
    public async Task Stop_failure_is_visible_even_when_policy_session_is_closed()
    {
        using var owner = New(); await owner.LoadAsync(0); player.FailStop = true; owner.Stop();
        Assert.True(owner.CleanupUnconfirmed); Assert.Equal(PlaybackState.Empty, owner.Session.State);
        player.FailStop = false; owner.Stop(); Assert.False(owner.CleanupUnconfirmed);
    }
    [Fact]
    public async Task Unavailable_entry_never_opens_native_media()
    {
        using var owner = New(); await owner.LoadAsync(1); Assert.Equal(0, player.Opens);
        Assert.Equal(PlaybackState.Unavailable, owner.Session.State);
    }
    [Fact]
    public async Task Disposal_unsubscribes_and_blocks_further_commands()
    {
        var owner = New(); await owner.LoadAsync(0); owner.Dispose(); owner.Dispose();
        Assert.Equal(0, player.Subscribers); Assert.Throws<ObjectDisposedException>(owner.Play);
    }
    [Fact]
    public async Task Seek_saves_actual_native_position_instead_of_requested_position()
    {
        using var owner = New(); await owner.LoadAsync(0); player.SeekAdjustment = -1;
        await owner.SeekAsync(TimeSpan.FromSeconds(20)); Assert.Equal(TimeSpan.FromSeconds(19).Ticks, store.Saved!.PositionTicks);
    }
    [Fact]
    public async Task Restore_uses_saved_identity_and_actual_seek_result_without_playing()
    {
        store.Saved = new("background-timing", "pcm-bg-v1", TimeSpan.FromSeconds(13).Ticks, TimeSpan.FromSeconds(60).Ticks, false);
        using var owner = New(); player.SeekAdjustment = -1; await owner.LoadAsync(0);
        Assert.Equal(TimeSpan.FromSeconds(12), owner.Session.Position); Assert.Equal(0, player.Plays);
    }
    private sealed class Store : IAudioCheckpointStore
    {
        public AudioCheckpoint? Saved; public bool Fail;
        public AudioCheckpoint? Read(AudioItem item) => Saved;
        public void Save(AudioCheckpoint checkpoint) { if (Fail) throw new IOException(); Saved = checkpoint; }
        public void Remove(AudioItem item) => Saved = null;
    }
    private sealed class Player : IAudioPlayer
    {
        private Action<PlaybackObservation>? observed;
        public int Subscribers { get; private set; }
        public event Action<PlaybackObservation>? Observed { add { observed += value; Subscribers++; } remove { observed -= value; Subscribers--; } }
        public TimeSpan Position { get; set; }
        public PlaybackTicket Ticket;
        public int Plays, Opens, Pauses, Stops, SeekAdjustment;
        public bool Delayed, IgnoreCancellation, FailStop;
        private readonly TaskCompletionSource<TimeSpan> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<TimeSpan> OpenAsync(AudioItem item, PlaybackTicket ticket, CancellationToken ct)
        {
            Ticket = ticket; Opens++; Position = TimeSpan.Zero;
            if (!Delayed) return Task.FromResult(TimeSpan.FromSeconds(60));
            return IgnoreCancellation ? ready.Task : ready.Task.WaitAsync(ct);
        }
        public void Complete() => ready.SetResult(TimeSpan.FromSeconds(60));
        public void Emit(PlaybackTicket ticket, PlaybackSignal signal, int seconds) => observed?.Invoke(new(ticket, signal, TimeSpan.FromSeconds(seconds)));
        public void Play() { Plays++; Emit(Ticket, PlaybackSignal.Playing, (int)Position.TotalSeconds); }
        public void Pause() { Pauses++; Emit(Ticket, PlaybackSignal.Paused, (int)Position.TotalSeconds); }
        public Task SeekAsync(TimeSpan target, CancellationToken ct) { Position = target + TimeSpan.FromSeconds(SeekAdjustment); return Task.CompletedTask; }
        public void Stop() { Stops++; if (FailStop) throw new IOException(); }
    }
}
