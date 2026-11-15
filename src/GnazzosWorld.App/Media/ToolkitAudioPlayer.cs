using CommunityToolkit.Maui.Views;
using GnazzosWorld.Core.Media;

namespace GnazzosWorld.App.Media;

/// <summary>UI-thread-only foreground adapter. Fresh element/closures for every source.</summary>
public sealed class ToolkitAudioPlayer(ContentView host) : IAudioPlayer
{
    private MediaElement? current;
    private TaskCompletionSource<TimeSpan>? opening;
    public event Action<PlaybackObservation>? Observed;
    public TimeSpan Position => current?.Position ?? TimeSpan.Zero;
    public async Task<TimeSpan> OpenAsync(AudioItem item, PlaybackTicket ticket, CancellationToken ct)
    {
        Stop(); ct.ThrowIfCancellationRequested();
        if (item.Resource is null) throw new InvalidOperationException("This media is unavailable.");
        var element = new MediaElement
        {
            ShouldAutoPlay = false, ShouldShowPlaybackControls = false, ShouldLoopPlayback = false,
            ShouldKeepScreenOn = false, Volume = 0.25, HeightRequest = 40,
            MetadataTitle = item.Title, MetadataArtist = "Original teaching fixture"
        };
        var ready = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
        current = element; opening = ready;
        void Emit(PlaybackSignal signal)
        {
            // Native event delivery may be queued. Recheck ownership on the UI dispatcher.
            host.Dispatcher.Dispatch(() =>
            {
                if (ReferenceEquals(current, element)) Observed?.Invoke(new(ticket, signal, element.Position));
            });
        }
        element.MediaOpened += (_, _) => { if (ReferenceEquals(current, element)) ready.TrySetResult(element.Duration); };
        element.MediaFailed += (_, _) =>
        {
            if (!ReferenceEquals(current, element)) return;
            ready.TrySetException(new InvalidDataException("The native player could not open the teaching media."));
            Emit(PlaybackSignal.Failed);
        };
        element.MediaEnded += (_, _) => Emit(PlaybackSignal.Ended);
        element.PositionChanged += (_, _) => Emit(PlaybackSignal.Position);
        element.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(MediaElement.CurrentState)) return;
            switch (element.CurrentState.ToString())
            {
                case "Playing": Emit(PlaybackSignal.Playing); break;
                case "Paused": Emit(PlaybackSignal.Paused); break;
                case "Buffering": Emit(PlaybackSignal.Buffering); break;
            }
        };
        host.Content = element;
        try
        {
            element.Source = MediaSource.FromResource(item.Resource);
            var duration = await ready.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
            ct.ThrowIfCancellationRequested();
            if (!ReferenceEquals(current, element)) throw new OperationCanceledException(ct);
            return duration;
        }
        catch
        {
            if (ReferenceEquals(current, element)) Stop();
            throw;
        }
    }
    public void Play() => (current ?? throw new InvalidOperationException("Load media first.")).Play();
    public void Pause() => current?.Pause();
    public Task SeekAsync(TimeSpan target, CancellationToken ct) =>
        (current ?? throw new InvalidOperationException("Load media first.")).SeekTo(target, ct);
    public void Stop()
    {
        var prior = current; current = null;
        opening?.TrySetCanceled(); opening = null; host.Content = null;
        if (prior is null) return;
        try { prior.Stop(); }
        finally
        {
            try { prior.Source = null; }
            finally { prior.Handler?.DisconnectHandler(); }
        }
    }
}
