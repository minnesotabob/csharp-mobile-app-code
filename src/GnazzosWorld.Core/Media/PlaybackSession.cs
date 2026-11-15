namespace GnazzosWorld.Core.Media;

public sealed record AudioItem
{
    public string Id { get; }
    public string Revision { get; }
    public string Title { get; }
    public string? Resource { get; }
    public AudioItem(string id, string revision, string title, string? resource)
    {
        if (!Key(id) || !Key(revision)) throw new ArgumentException("Use bounded media identities.");
        if (string.IsNullOrWhiteSpace(title) || title.Length > 120 || title.Any(char.IsControl))
            throw new ArgumentException("Use a bounded media title.");
        if (resource is not null && (resource.Length > 80 || !resource.EndsWith(".wav", StringComparison.Ordinal) ||
            resource.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '_' and not '.')))
            throw new ArgumentException("Use a bundled WAV resource name, not a URL or path.");
        Id = id; Revision = revision; Title = title; Resource = resource;
    }
    internal static bool Key(string? value) => !string.IsNullOrEmpty(value) && value.Length <= 64 &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    public override string ToString() => Title;
}
public enum PlaybackState { Empty, Loading, Unavailable, Ready, Playing, Paused, Buffering, Ended, Failed }
public enum PlaybackSignal { Position, Playing, Paused, Buffering, Ended, Failed }
public readonly record struct PlaybackTicket(long Generation);
public readonly record struct PlaybackObservation(PlaybackTicket Ticket, PlaybackSignal Signal, TimeSpan Position);
public sealed record AudioCheckpoint(string ItemId, string Revision, long PositionTicks, long DurationTicks, bool Completed);

/// <summary>Foreground device port. Its implementation owns native resources and actual observations.</summary>
public interface IAudioPlayer
{
    event Action<PlaybackObservation>? Observed;
    TimeSpan Position { get; }
    Task<TimeSpan> OpenAsync(AudioItem item, PlaybackTicket ticket, CancellationToken ct);
    void Play();
    void Pause();
    Task SeekAsync(TimeSpan target, CancellationToken ct);
    void Stop();
}

/// <summary>UI-thread-affine policy; no native player, account authentication or automatic background work.</summary>
public sealed class PlaybackSession
{
    public static readonly TimeSpan MaximumDuration = TimeSpan.FromHours(24);
    private long generation;
    public IReadOnlyList<AudioItem> Queue { get; }
    public int Index { get; private set; } = -1;
    public AudioItem? Current => Index < 0 ? null : Queue[Index];
    public PlaybackTicket Ticket => new(generation);
    public PlaybackState State { get; private set; }
    public TimeSpan Duration { get; private set; }
    public TimeSpan Position { get; private set; }
    public bool CanPlay => State is PlaybackState.Ready or PlaybackState.Paused or PlaybackState.Ended;
    public bool CanSeek => State is PlaybackState.Ready or PlaybackState.Playing or PlaybackState.Paused;
    public PlaybackSession(IEnumerable<AudioItem> queue)
    {
        var items = queue.ToArray();
        if (items.Length is < 1 or > 32 || items.Any(x => x is null) || items.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != items.Length)
            throw new ArgumentException("Use one bounded queue with unique stable IDs.", nameof(queue));
        Queue = Array.AsReadOnly(items);
    }
    public PlaybackTicket BeginSelection(int index)
    {
        if (index < 0 || index >= Queue.Count) throw new ArgumentOutOfRangeException(nameof(index));
        generation = checked(generation + 1); Index = index; Duration = Position = TimeSpan.Zero;
        State = Current!.Resource is null ? PlaybackState.Unavailable : PlaybackState.Loading;
        return Ticket;
    }
    public bool IsCurrent(PlaybackTicket ticket) => ticket == Ticket && State != PlaybackState.Empty;
    public TimeSpan ResumeTarget(PlaybackTicket ticket, TimeSpan duration, AudioCheckpoint? saved)
    {
        if (!IsCurrent(ticket) || State != PlaybackState.Loading) throw new InvalidOperationException("Opening belongs to an obsolete selection.");
        CheckDuration(duration);
        if (saved is null || saved.ItemId != Current!.Id || saved.Revision != Current.Revision || saved.Completed)
            return TimeSpan.Zero;
        ValidateCheckpoint(saved);
        var target = TimeSpan.FromTicks(saved.PositionTicks);
        return target >= duration ? TimeSpan.Zero : target;
    }
    public bool Ready(PlaybackTicket ticket, TimeSpan duration, TimeSpan observedPosition)
    {
        if (!IsCurrent(ticket) || State != PlaybackState.Loading) return false;
        CheckDuration(duration); Duration = duration;
        Position = Clamp(observedPosition); State = PlaybackState.Ready; return true;
    }
    public void Observe(PlaybackObservation observation)
    {
        if (!IsCurrent(observation.Ticket) || State is PlaybackState.Unavailable or PlaybackState.Failed or PlaybackState.Ended) return;
        if (observation.Signal == PlaybackSignal.Failed) { State = PlaybackState.Failed; return; }
        if (State == PlaybackState.Loading) return; // Ready/restore is an explicit completed operation.
        switch (observation.Signal)
        {
            case PlaybackSignal.Position:
                if (State != PlaybackState.Ended) Position = Clamp(observation.Position);
                break;
            case PlaybackSignal.Playing: State = PlaybackState.Playing; break;
            case PlaybackSignal.Paused:
                if (State != PlaybackState.Ended) State = PlaybackState.Paused;
                break;
            case PlaybackSignal.Buffering:
                if (State != PlaybackState.Ended) State = PlaybackState.Buffering;
                break;
            case PlaybackSignal.Ended: Position = Duration; State = PlaybackState.Ended; break;
        }
    }
    public TimeSpan SeekTarget(TimeSpan requested)
    {
        if (!CanSeek) throw new InvalidOperationException("Wait until the media is ready before seeking.");
        return Clamp(requested);
    }
    public AudioCheckpoint? Checkpoint() => Current is null || Duration <= TimeSpan.Zero ? null :
        new(Current.Id, Current.Revision, Position.Ticks, Duration.Ticks, State == PlaybackState.Ended);
    public void Close()
    {
        generation = checked(generation + 1); State = PlaybackState.Empty; Index = -1;
        Duration = Position = TimeSpan.Zero;
    }
    private TimeSpan Clamp(TimeSpan position) => position < TimeSpan.Zero ? TimeSpan.Zero : position > Duration ? Duration : position;
    private static void CheckDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero || duration > MaximumDuration) throw new InvalidDataException("Expected finite on-demand media with a bounded duration.");
    }
    public static void ValidateCheckpoint(AudioCheckpoint value)
    {
        if (!AudioItem.Key(value.ItemId) || !AudioItem.Key(value.Revision) || value.DurationTicks <= 0 ||
            value.DurationTicks > MaximumDuration.Ticks || value.PositionTicks < 0 || value.PositionTicks > value.DurationTicks ||
            (value.Completed && value.PositionTicks != value.DurationTicks))
            throw new InvalidDataException("Invalid audio checkpoint.");
    }
}

public static class DemoAudio
{
    public static IReadOnlyList<AudioItem> Items { get; } = Array.AsReadOnly(new[]
    {
        new AudioItem("demo-chimes", "pcm-v1", "Original timing chimes (10 seconds)", "demo_chimes.wav"),
        new AudioItem("demo-pulses", "pcm-v1", "Original timing pulses (14 seconds)", "demo_pulses.wav"),
        new AudioItem("unavailable-example", "v1", "Unavailable episode demonstration", null)
    });
}
