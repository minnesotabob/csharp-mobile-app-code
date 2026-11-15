namespace GnazzosWorld.App.Media;

/// <summary>Additional iOS pause-only interruption policy; Android delegates focus/routes to Media3.</summary>
public sealed class NativeAudioInterruptionMonitor : IDisposable
{
#if IOS
    private readonly IDisposable interruption;
    private readonly IDisposable route;
#endif
    public NativeAudioInterruptionMonitor(Action pause)
    {
#if IOS
        interruption = AVFoundation.AVAudioSession.Notifications.ObserveInterruption((_, args) =>
        {
            if (args.InterruptionType == AVFoundation.AVAudioSessionInterruptionType.Began) pause();
        });
        route = AVFoundation.AVAudioSession.Notifications.ObserveRouteChange((_, args) =>
        {
            if (args.Reason == AVFoundation.AVAudioSessionRouteChangeReason.OldDeviceUnavailable) pause();
        });
#endif
    }
    public void Dispose()
    {
#if IOS
        interruption.Dispose(); route.Dispose();
#endif
    }
}
