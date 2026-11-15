namespace GnazzosWorld.Core.Media;

/// <summary>Single-window/UI-thread owner. Native session commands may arrive without a visible control.</summary>
public sealed class BackgroundPlaybackOwner : IDisposable
{
    private readonly IAudioPlayer player;
    private readonly IAudioCheckpointStore progress;
    private CancellationTokenSource? pending;
    private bool disposed;
    public PlaybackSession Session { get; }
    public bool InForeground { get; private set; } = true;
    public bool AllowBackground { get; private set; }
    public bool Busy { get; private set; }
    public string Message { get; private set; } = "Background continuation is off. Load from the foreground, then choose Play.";
    public bool SaveUnconfirmed { get; private set; }
    public bool CleanupUnconfirmed { get; private set; }
    public event Action? Changed;
    public BackgroundPlaybackOwner(IAudioPlayer player, IAudioCheckpointStore progress, IEnumerable<AudioItem> queue)
    {
        this.player = player; this.progress = progress; Session = new(queue);
        player.Observed += Observe;
    }
    public void SetBackgroundAllowed(bool enabled)
    {
        Check(); AllowBackground = enabled;
        if (!enabled && !InForeground) Stop();
        Message = enabled ? "Existing playback may continue while this lab is backgrounded. Leaving the lab stops it." : "Background continuation is off.";
        Notify();
    }
    public void SetForeground(bool foreground)
    {
        Check(); InForeground = foreground;
        if (!foreground)
        {
            CapturePosition(); Save();
            if (!AllowBackground || Session.State == PlaybackState.Loading) Stop();
        }
        // Returning to the foreground is never an automatic Play or Load command.
        Notify();
    }
    public async Task LoadAsync(int index, CancellationToken ct = default)
    {
        Check();
        if (!InForeground) throw new InvalidOperationException("Begin new media from a foreground user action.");
        if (Busy) throw new InvalidOperationException("Another player action is still finishing.");
        Busy = true;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct); pending = cancellation;
        var token = cancellation.Token;
        try
        {
            StopNativeAndClose();
            var ticket = Session.BeginSelection(index); Notify();
            if (Session.State == PlaybackState.Unavailable) { Message = "Selected media is unavailable."; return; }
            var item = Session.Current!; var saved = progress.Read(item);
            var duration = await player.OpenAsync(item, ticket, token);
            token.ThrowIfCancellationRequested();
            var target = Session.ResumeTarget(ticket, duration, saved);
            if (target > TimeSpan.Zero) await player.SeekAsync(target, token).WaitAsync(TimeSpan.FromSeconds(10), token);
            token.ThrowIfCancellationRequested();
            if (Session.Ready(ticket, duration, player.Position)) Message = "Ready without autoplay. Choose Play.";
        }
        catch (OperationCanceledException)
        {
            StopNativeAndClose(); Message = "Opening canceled; no automatic restart."; throw;
        }
        catch
        {
            StopNativeAndClose(); Message = "Media could not be prepared. Reload deliberately from the foreground."; throw;
        }
        finally { pending = null; Busy = false; Notify(); }
    }
    public void Play()
    {
        Check();
        if (Busy || !Session.CanPlay || (!InForeground && !AllowBackground)) return;
        player.Play(); Message = "Play requested; native state confirms playback."; Notify();
    }
    public void Pause()
    {
        Check(); player.Pause(); CapturePosition(); Save(); Message = "Pause requested. No app-initiated automatic resume."; Notify();
    }
    public async Task SeekAsync(TimeSpan requested, CancellationToken ct = default)
    {
        Check();
        if (Busy) throw new InvalidOperationException("Another player action is still finishing.");
        var target = Session.SeekTarget(requested); var ticket = Session.Ticket;
        Busy = true; using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct); pending = cancellation;
        try
        {
            await player.SeekAsync(target, cancellation.Token).WaitAsync(TimeSpan.FromSeconds(10), cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            Session.Observe(new(ticket, PlaybackSignal.Position, player.Position)); Save();
        }
        catch { StopNativeAndClose(); Message = "Seek did not complete; the source was released."; throw; }
        finally { pending = null; Busy = false; Notify(); }
    }
    public void Checkpoint() { Check(); CapturePosition(); Save(); Notify(); }
    public void Stop()
    {
        Check(); pending?.Cancel(); StopNativeAndClose(); Message = "Stopped. Explicit foreground load is required to restart."; Notify();
    }
    private void StopNativeAndClose()
    {
        CapturePosition(); Save(); Session.Close();
        try { player.Stop(); CleanupUnconfirmed = false; }
        catch { CleanupUnconfirmed = true; }
    }
    private void Observe(PlaybackObservation observation)
    {
        if (disposed || !Session.IsCurrent(observation.Ticket)) return;
        Session.Observe(observation);
        if (Session.State is PlaybackState.Ended or PlaybackState.Failed)
        {
            var ended = Session.State == PlaybackState.Ended;
            Save(); Session.Close();
            try { player.Stop(); CleanupUnconfirmed = false; } catch { CleanupUnconfirmed = true; }
            Message = ended ? "Completed. Session released; load again from the foreground to replay." : "Native playback failed; session released.";
        }
        else if (!InForeground && !AllowBackground && Session.State == PlaybackState.Playing) Stop();
        else if (observation.Signal == PlaybackSignal.Paused) Save();
        Notify();
    }
    private void CapturePosition()
    {
        if (Session.Duration > TimeSpan.Zero)
            Session.Observe(new(Session.Ticket, PlaybackSignal.Position, player.Position));
    }
    private void Save()
    {
        if (Session.Checkpoint() is not { } checkpoint) return;
        try { progress.Save(checkpoint); SaveUnconfirmed = false; }
        catch { SaveUnconfirmed = true; }
    }
    private void Notify() => Changed?.Invoke();
    private void Check() => ObjectDisposedException.ThrowIf(disposed, this);
    public void Dispose()
    {
        if (disposed) return;
        pending?.Cancel(); StopNativeAndClose(); player.Observed -= Observe;
        disposed = true; Changed = null;
    }
}

public static class BackgroundDemoAudio
{
    public static IReadOnlyList<AudioItem> Items { get; } = Array.AsReadOnly(new[]
    {
        new AudioItem("background-timing", "pcm-bg-v1", "Original background timing fixture (60 seconds)", "background_timing.wav"),
        new AudioItem("background-unavailable", "v1", "Unavailable background example", null)
    });
}
