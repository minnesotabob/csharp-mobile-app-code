using System.Runtime.CompilerServices;
using GnazzosWorld.Core.Media;

namespace GnazzosWorld.PerformanceLab;

internal static class RetentionMeasurements
{
    internal static object Run()
    {
        var publisher = new Publisher(); var observers = Create(publisher);
        Collect(); var before = Alive(observers); var subscriptionsBefore = publisher.Subscribers;
        Release(observers); Collect(); var after = Alive(observers); var subscriptionsAfter = publisher.Subscribers;
        GC.KeepAlive(publisher);
        if (subscriptionsBefore != 32 || subscriptionsAfter != 0) throw new InvalidDataException("Subscription ownership did not match.");
        return new { Owners = 32, SubscriptionsBefore = subscriptionsBefore, SubscriptionsAfter = subscriptionsAfter,
            WeakOwnersBefore = before, WeakOwnersAfter = after, SyntheticPayloadBytesPerOwner = 65536,
            Scope = "Controlled omitted-Dispose experiment on existing BackgroundPlaybackOwner; weak counts are observations, not native leak/erasure proof." };
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<BackgroundPlaybackOwner>[] Create(Publisher publisher)
    {
        var result = new WeakReference<BackgroundPlaybackOwner>[32];
        for (var i = 0; i < result.Length; i++)
        {
            var owner = new BackgroundPlaybackOwner(publisher, new Checkpoints(), BackgroundDemoAudio.Items);
            var payload = new byte[65536]; owner.Changed += () => GC.KeepAlive(payload);
            result[i] = new(owner);
        }
        return result;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Release(WeakReference<BackgroundPlaybackOwner>[] owners)
    { foreach (var weak in owners) if (weak.TryGetTarget(out var target)) target.Dispose(); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Alive(WeakReference<BackgroundPlaybackOwner>[] owners)
        => owners.Count(w => w.TryGetTarget(out _));
    private static void Collect() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
    private sealed class Publisher : IAudioPlayer
    {
        private Action<PlaybackObservation>? observed;
        public int Subscribers { get; private set; }
        public event Action<PlaybackObservation>? Observed
        { add { observed += value; Subscribers++; } remove { observed -= value; Subscribers--; } }
        public TimeSpan Position => TimeSpan.Zero;
        public Task<TimeSpan> OpenAsync(AudioItem item, PlaybackTicket ticket, CancellationToken ct) => Task.FromResult(TimeSpan.FromSeconds(1));
        public void Play() { } public void Pause() { } public void Stop() { }
        public Task SeekAsync(TimeSpan target, CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class Checkpoints : IAudioCheckpointStore
    { public AudioCheckpoint? Read(AudioItem item) => null; public void Save(AudioCheckpoint checkpoint) { } public void Remove(AudioItem item) { } }
}
