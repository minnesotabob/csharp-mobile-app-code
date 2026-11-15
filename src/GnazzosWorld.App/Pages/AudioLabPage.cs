using GnazzosWorld.App.Media;
using GnazzosWorld.Core.Media;

namespace GnazzosWorld.App.Pages;

/// <summary>Anonymous foreground playback only. One page owns its player and source epoch.</summary>
public sealed class AudioLabPage : ContentPage
{
    private readonly PlaybackSession session = new(DemoAudio.Items);
    private readonly AudioProgressDatabase progress;
    private readonly IAudioPlayer player;
    private readonly Picker queue = new() { Title = "Choose an original audio fixture" };
    private readonly Label status = new();
    private readonly Label position = new();
    private readonly Label persistence = new();
    private readonly Button load = new() { Text = "Load selected" };
    private readonly Button play = new() { Text = "Play" };
    private readonly Button pause = new() { Text = "Pause and save" };
    private readonly Button backward = new() { Text = "Back 5 seconds" };
    private readonly Button forward = new() { Text = "Forward 5 seconds" };
    private readonly Button previous = new() { Text = "Previous item" };
    private readonly Button next = new() { Text = "Next item" };
    private readonly Button forget = new() { Text = "Forget selected progress" };
    private readonly IDispatcherTimer timer;
    private CancellationTokenSource? pending;
    private Window? observedWindow;
    private bool busy;
    private bool visible;
    private DateTimeOffset lastSave;

    public AudioLabPage(AudioProgressDatabase progress)
    {
        this.progress = progress; Title = "Foreground audio lab";
        var host = new ContentView(); player = new ToolkitAudioPlayer(host);
        queue.ItemsSource = DemoAudio.Items.ToList(); queue.SelectedIndex = 0;
        var heading = new Label { Text = "Audio and podcast-progress foundations", FontSize = 25 };
        SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
        SemanticProperties.SetDescription(position, "Observed playback position and duration");
        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = 24, Spacing = 12,
            Children = { heading,
                new Label { Text = "Original timing signals, not published songs or podcast recordings. Foreground only; no autoplay or background service. Progress is anonymous to this installation, not account-synchronized." },
                queue, load, host, status, position, persistence, play, pause, backward, forward, previous, next, forget }
        }};
        load.Clicked += async (_, _) => await RunAsync(LoadAsync);
        play.Clicked += async (_, _) => await RunAsync(async ct =>
        {
            if (!session.CanPlay) return;
            if (session.State == PlaybackState.Ended)
            {
                queue.SelectedIndex = session.Index;
                await LoadAsync(ct); // New element/ticket: completion is terminal for the old source.
            }
            ct.ThrowIfCancellationRequested(); player.Play(); // Playing is confirmed by a device observation.
        });
        pause.Clicked += async (_, _) => await RunAsync(_ => { player.Pause(); CapturePosition(); Save(); return Task.CompletedTask; });
        backward.Clicked += async (_, _) => await RunAsync(ct => SeekAsync(-5, ct));
        forward.Clicked += async (_, _) => await RunAsync(ct => SeekAsync(5, ct));
        previous.Clicked += async (_, _) => await MoveAsync(-1);
        next.Clicked += async (_, _) => await MoveAsync(1);
        forget.Clicked += async (_, _) => await RunAsync(_ =>
        {
            Suspend(); // Do not let the active timer immediately recreate the removed checkpoint.
            progress.Remove(DemoAudio.Items[queue.SelectedIndex]);
            persistence.Text = "Selected progress removed from this installation."; return Task.CompletedTask;
        });
        player.Observed += observation =>
        {
            session.Observe(observation);
            if (session.IsCurrent(observation.Ticket) && observation.Signal == PlaybackSignal.Ended) TrySave();
            if (session.IsCurrent(observation.Ticket) && observation.Signal == PlaybackSignal.Failed)
            {
                TrySave();
                try { player.Stop(); }
                catch { persistence.Text = "Native failure cleanup could not be confirmed on this device."; }
            }
            Refresh();
        };
        timer = Dispatcher.CreateTimer(); timer.Interval = TimeSpan.FromSeconds(5);
        timer.Tick += (_, _) => { if (!busy && visible) TrySave(); };
        queue.SelectedIndexChanged += (_, _) => Refresh();
        Refresh();
    }
    protected override void OnAppearing()
    {
        base.OnAppearing(); visible = true; observedWindow = Window;
        if (observedWindow is not null) observedWindow.Stopped += OnWindowStopped;
        timer.Start(); Refresh();
    }
    protected override void OnDisappearing()
    {
        visible = false; timer.Stop();
        if (observedWindow is not null) observedWindow.Stopped -= OnWindowStopped;
        observedWindow = null; Suspend(); base.OnDisappearing();
    }
    private void OnWindowStopped(object? sender, EventArgs args) => Suspend();
    private void Suspend()
    {
        pending?.Cancel();
        try { player.Pause(); CapturePosition(); TrySave(); }
        catch { persistence.Text = "Native pause could not be confirmed; cleanup was requested."; }
        finally
        {
            session.Close();
            try { player.Stop(); }
            catch { persistence.Text = "Native player cleanup needs verification on this device."; }
            Refresh();
        }
    }
    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (busy || !visible) return;
        busy = true; using var cancellation = new CancellationTokenSource(); pending = cancellation; Refresh();
        try { await action(cancellation.Token); }
        catch (OperationCanceledException) { }
        catch
        {
            session.Observe(new(session.Ticket, PlaybackSignal.Failed, TimeSpan.Zero));
            try { player.Stop(); } catch { /* Keep the user-facing outcome sanitized and unconfirmed. */ }
            persistence.Text = "The action could not complete. Stored progress has not been confirmed. You can reload or forget the selected checkpoint.";
        }
        finally { pending = null; busy = false; Refresh(); }
    }
    private async Task MoveAsync(int delta)
    {
        var target = queue.SelectedIndex + delta;
        if (target < 0 || target >= DemoAudio.Items.Count) return;
        await RunAsync(async ct => { CapturePosition(); Save(); queue.SelectedIndex = target; await LoadAsync(ct); });
    }
    private async Task LoadAsync(CancellationToken ct)
    {
        player.Pause(); CapturePosition(); Save(); player.Stop();
        var item = DemoAudio.Items[queue.SelectedIndex]; var ticket = session.BeginSelection(queue.SelectedIndex); Refresh();
        if (session.State == PlaybackState.Unavailable) return;
        try
        {
            var saved = progress.Read(item);
            var duration = await player.OpenAsync(item, ticket, ct);
            ct.ThrowIfCancellationRequested();
            var resume = session.ResumeTarget(ticket, duration, saved);
            if (resume > TimeSpan.Zero) await player.SeekAsync(resume, ct).WaitAsync(TimeSpan.FromSeconds(10), ct);
            ct.ThrowIfCancellationRequested();
            if (session.Ready(ticket, duration, player.Position))
                persistence.Text = "Loaded without autoplay. Position reflects the native player; choose Play.";
        }
        catch
        {
            session.Observe(new(ticket, PlaybackSignal.Failed, TimeSpan.Zero)); player.Stop(); throw;
        }
    }
    private async Task SeekAsync(int seconds, CancellationToken ct)
    {
        var ticket = session.Ticket; var target = session.SeekTarget(session.Position + TimeSpan.FromSeconds(seconds));
        await player.SeekAsync(target, ct).WaitAsync(TimeSpan.FromSeconds(10), ct); ct.ThrowIfCancellationRequested();
        session.Observe(new(ticket, PlaybackSignal.Position, player.Position)); Save();
    }
    private void CapturePosition()
    {
        if (session.Duration > TimeSpan.Zero) session.Observe(new(session.Ticket, PlaybackSignal.Position, player.Position));
    }
    private void Save()
    {
        var checkpoint = session.Checkpoint(); if (checkpoint is null) return;
        progress.Save(checkpoint); lastSave = DateTimeOffset.UtcNow;
        persistence.Text = "Progress saved locally at " + lastSave.ToLocalTime().ToString("T") + ".";
    }
    private void TrySave()
    {
        try { Save(); }
        catch { persistence.Text = "Playback progress could not be saved. Earlier confirmed progress may be restored."; }
    }
    private void Refresh()
    {
        status.Text = session.State + (session.Current is null ? "" : ": " + session.Current.Title);
        position.Text = $"{session.Position.TotalSeconds:0.0} / {session.Duration.TotalSeconds:0.0} seconds";
        queue.IsEnabled = load.IsEnabled = forget.IsEnabled = !busy;
        play.IsEnabled = !busy && session.CanPlay;
        pause.IsEnabled = !busy && session.State is PlaybackState.Playing or PlaybackState.Buffering or PlaybackState.Paused;
        backward.IsEnabled = forward.IsEnabled = !busy && session.CanSeek;
        previous.IsEnabled = !busy && queue.SelectedIndex > 0;
        next.IsEnabled = !busy && queue.SelectedIndex < DemoAudio.Items.Count - 1;
    }
}
