using CommunityToolkit.Maui.Core.Handlers;
using CommunityToolkit.Maui.Views;

namespace GnazzosWorld.App.Media;

public sealed class BackgroundMediaElement : MediaElement { }

/// <summary>Use the library's native player; do not request competing audio focus in application code.</summary>
public sealed class FocusAwareMediaElementHandler : MediaElementHandler
{
#if ANDROID
    protected override CommunityToolkit.Maui.Core.Views.MauiMediaElement CreatePlatformView()
    {
        var view = base.CreatePlatformView();
        if (MediaManager?.Player is { } native)
        {
            // Android/Media3 public constants: USAGE_MEDIA=1, CONTENT_TYPE_SPEECH=1.
            var attributes = new AndroidX.Media3.Common.AudioAttributes.Builder()
                .SetUsage(1).SetContentType(1).Build();
            native.SetAudioAttributes(attributes, true); // Media3 owns gain/loss and user play intent.
            native.SetHandleAudioBecomingNoisy(true); // Pause when private audio output disconnects.
        }
        return view;
    }
#endif
}
