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
        // Locate the public native PlayerView in the toolkit host; no private fields/reflection.
        // Fail preparation visibly if the pinned toolkit's view structure changes.
        var playerView = FindPlayerView(view) ?? throw new InvalidOperationException("Native player view was not found.");
        if (playerView.Player is AndroidX.Media3.ExoPlayer.IExoPlayer native)
        {
            // Android/Media3 public constants: USAGE_MEDIA=1, CONTENT_TYPE_SPEECH=1.
            using var attributesBuilder = new AndroidX.Media3.Common.AudioAttributes.Builder();
            attributesBuilder.SetUsage(1);
            attributesBuilder.SetContentType(1);
            var attributes = attributesBuilder.Build()
                ?? throw new InvalidOperationException("Audio attributes could not be created.");
            native.SetAudioAttributes(attributes, true); // Media3 owns gain/loss and user play intent.
            native.SetHandleAudioBecomingNoisy(true); // Pause when private audio output disconnects.
        }
        else throw new InvalidOperationException("Expected the pinned Media3 player implementation.");
        return view;
    }
    private static AndroidX.Media3.UI.PlayerView? FindPlayerView(global::Android.Views.View view)
    {
        if (view is AndroidX.Media3.UI.PlayerView playerView) return playerView;
        if (view is global::Android.Views.ViewGroup group)
            for (var i = 0; i < group.ChildCount; i++)
                if (group.GetChildAt(i) is { } child && FindPlayerView(child) is { } found) return found;
        return null;
    }
#endif
}
