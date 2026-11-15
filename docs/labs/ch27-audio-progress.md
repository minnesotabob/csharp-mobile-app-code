# Chapter 27 - foreground audio and local progress

## Implemented scope

Portable PlaybackSession policy, IAudioPlayer port, real SQLite AudioProgressDatabase,
MAUI ToolkitAudioPlayer adapter and Home > Open foreground audio lab. Only two bundled,
original PCM timing WAVs are playable; a third queue entry is deliberately unavailable.
No Spotify/Apple page is treated as audio. No private media, remote stream, download,
account synchronization, background service or lock-screen control is enabled here.

Starting checkpoint: 7a32c46e1246352fcc275ebf52d920bb2a3ccefd. Implementation starts at
d500294db4b3dead74ba7952fa6bf85b5071a973; use corrective source/evidence in verification.md
if present. Package candidate: CommunityToolkit.Maui.MediaElement 12.0.0 with the pinned
.NET 11 / MAUI 11 baseline. Android minimum changes from API 24 to 26 for this media
capability; earlier chapter checkpoints keep their own minimum. iOS build/runtime and
its native SDK/package compatibility remain unverified until built on a matching Mac.

## Source map

- src/GnazzosWorld.Core/Media/PlaybackSession.cs: bounded queue, media revision, source ticket,
  state observations, restore target, observed-position checkpoint, terminal completion/failure.
- src/GnazzosWorld.Core/Media/AudioProgressDatabase.cs: separate anonymous-device SQLite file.
- src/GnazzosWorld.App/Media/ToolkitAudioPlayer.cs: fresh MediaElement for each source,
  bounded open/seek waits, native event translation, ownership recheck and cleanup.
- src/GnazzosWorld.App/Pages/AudioLabPage.cs: foreground controls, 5-second checkpoints,
  pause/save, manual previous/next, explicit replay, Window.Stopped/navigation cleanup.
- tests/GnazzosWorld.Core.Tests/PlaybackTests.cs: simulated observations plus actual file tests.
- eng/generate_demo_audio.py and samples/audio-fixtures.json: original resource generation
  and format/duration/SHA-256 provenance. No third-party recording is sampled.

## Portable verification

```powershell
dotnet build tests/GnazzosWorld.Core.Tests -c Release
dotnet run --project tests/GnazzosWorld.Core.Tests -c Release --no-build
```

The tests do not play sound. They distinguish desired seek from observed position, reject
late source events and A-B-A tickets, keep ended/failed states terminal, restore only the
matching item/revision, allow deliberate rewind and preserve the previous confirmed save
under an injected real SQLite error. Separate source-fixture tests inspect PCM headers,
duration and hashes; they do not establish native decoding or the packaged resource's path.

The anonymous audio database does not use the existing reading percent or authenticated
workspace. It is not encrypted, account-scoped or synchronized. Small synchronous SQLite
writes are a teaching limitation; investigate native responsiveness and contention before
using a larger workload. One page/coordinator owns observed state. There is no cross-window
or cross-process coordination guarantee and no atomic save at arbitrary process death.

## Native setup and run

1. Restore/build using global.json and the documented Android SDK/JDK/workload. The app
   references MediaElement 12.0.0, registers the CommunityToolkit.Maui builder extension with
   isAndroidForegroundServiceEnabled: false and packages Resources/Raw files as MauiAsset items. The activity uses the
   documented resize/single-task setup. No background audio entitlement/service is claimed.
2. Launch on an Android API-26-or-newer device. Compilation is separate from actual launch,
   decoder behavior and listening. Record app commit, package version, device/OS and volume.
3. Home > Open foreground audio lab. Choose the 10-second timing chimes and Load selected.
   Expected: Loading then Ready; no autoplay. Press Play. Expected: actual timing tones and
   observed Playing/position. A label alone does not prove audible output.
4. Pause and save midway. Reload: expected observed restored position and no autoplay.
   The player may round a seek; the page uses actual Position after completion. Use Back/
   Forward 5 seconds and compare observed time with the audible timing pattern.
5. Let it end. Expected completed checkpoint. Play again intentionally opens a fresh source
   at zero, not an old source revived by a queued event. Queue progression is manual;
   completion does not automatically start the next item.
6. Load the other original fixture, then the unavailable entry. Expected no stale title,
   duration or completion for the previous source; unavailable media never becomes playable.
7. Navigate away and send the app to the background independently. Expected pause/save
   attempt, source invalidation and native stop/disconnect. Return and explicitly load/play;
   never infer background/phone-call behavior from the foreground rule.
8. While loading/seeking, navigate away or background the app. Expected cancellation and no
   late reactivation. Seek/open errors make the source unusable until explicit reload.
9. Use Forget selected progress. It first suspends active playback so a timer cannot
   immediately recreate the checkpoint. A later deliberate new playback can save new progress.
10. Test large text, screen reader labels/focus, keyboard/navigation and platform audio volume.
    Controls are English-only in this lab; localization remains follow-through work.

## Failure and recovery record

Use a disposable development installation/database. A missing/corrupt source or unsupported
codec must show an actionable failure, not a successful completed episode. The automatic
open wait is 20 seconds and explicit seek waits are 10 seconds. Cancellation/timeout stops
the source; native codec cancellation and disposal still need hardware observation.

For a controlled progress write failure, preserve the earlier row and show that saving is
unconfirmed. Load restores only the last confirmed write. Corrupt checkpoint input is
rejected; Forget selected progress offers an explicit reset. The automated trigger test
proves SQLite rollback for its statement, not phone storage exhaustion or keychain behavior.

Record: source, packages, device/OS, item/revision, intended action, observed native state,
audible result, checkpoint readback and next action. Capture screenshots only of fictitious
media. Actual playback, seeks, app switching, interruption, iOS and screenshots remain pending
until observed. Background audio/focus/headphone routes belong to Chapter 28's separate work.

## Media provenance and rights

The two WAVs are original mathematical timing signals produced by the included script:
mono PCM, 16-bit, 8000 Hz, 10/14 seconds, with short quiet tapered tones every two seconds.
They are intentionally simple decoder/timeline fixtures, not production-quality music or
spoken podcasts. They may be redistributed with this teaching sample. The manifest records
source bytes; decoder/format support varies by platform and must be tested.

For real music or episodes, obtain distributable media and accurate metadata. Use a new
media revision when bytes/timeline change. A public streaming-service page or the ability
to listen in its app does not supply a playable/distributable file for this native player.
Remote streaming requires a reviewed HTTPS media endpoint, native network/caching evidence
and, for private deliveries, the Chapter 26 owner/transport contract before implementation.

Sources: https://learn.microsoft.com/dotnet/communitytoolkit/maui/views/mediaelement and
https://www.nuget.org/packages/CommunityToolkit.Maui.MediaElement/12.0.0 . Current docs and
API reference pages can describe different package versions; matching compilation and
actual native observations remain the decisive evidence for this sample.
