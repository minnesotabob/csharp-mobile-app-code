# Chapter 28 — background audio and system controls

Home → Background audio lab uses original `background_timing.wav` (60 seconds), not Minnesota Bob recordings or private deliveries. Load is foreground-only and never autoplays. Existing playback may continue after Window.Stopped only with explicit opt-in. A pending load is canceled on stop even with opt-in. Return never issues Load or Play. Disable opt-in while backgrounded: stop and release. Shell departure, window destruction, end and failure release the lab's source/session.

## Ownership and native integration

- `BackgroundPlaybackOwner` is serialized on the UI thread; it owns policy, source tickets and checkpoint requests. `IAudioCheckpointStore` separates storage from player orchestration.
- `ToolkitAudioPlayer` owns one native element. Background instances explicitly enable the toolkit Android service; Chapter 27 instances explicitly disable it. Global registration enables capability.
- The internal browse panel is a sibling of the mounted player host. Arbitrary Shell navigation stops playback. This is not application-wide navigation-independent audio.
- Toolkit 12.0.0 owns native sessions/remote commands. Its `MediaManager.Player` is inaccessible to this app. `FocusAwareMediaElementHandler` walks the public native view hierarchy to find Media3 `PlayerView`, configures its `IExoPlayer`, and throws visibly if the expected public view/player is absent. This is a pinned-version compatibility dependency; verify it when upgrading. No private reflection or duplicate focus/session owner.
- Media3 receives usage media/content speech, focus handling true and becoming-noisy handling true. Its native focus recovery policy can differ from application-issued resume commands; verify both.
- iOS declares background audio. Toolkit owns audio category and remote targets; the app observes interruption-began and old-device-unavailable and requests Pause on the main thread. It adds no app auto-resume. iOS compilation remains pending.
- Metadata contains public fixture titles only. No private request/customer content appears on the lock screen.
- Five-second foreground checkpointing is best effort. Window-stop, native pause, explicit pause/seek/stop and terminal events save where possible. No background timer, process-survival or final-save guarantee.
- Save and cleanup uncertainty are separate visible flags. Reload can restore an earlier confirmed checkpoint. Anonymous progress is unencrypted installation-local SQLite, not account-synchronized or private-delivery storage.

## Acceptance worksheet

All native rows are **pending** until performed. Record app SHA, model, OS, output route, steps, actual result and evidence; compile success is not a pass here.

| Experiment | Expected application contract | Android | iPhone |
| --- | --- | --- | --- |
| Load in foreground | Ready without autoplay; explicit Play needed | Pending | Pending |
| Internal browse panel | Mounted host retained; no second source open | Pending | Pending |
| Background without opt-in | Cancel/close; no restart on return | Pending | Pending |
| Background during load with opt-in | Cancel; delayed open cannot publish Ready | Pending | Pending |
| Background during playback with opt-in | Existing source eligible to continue; system constraints apply | Pending | Pending |
| Lock-screen metadata | Public fixture only; accurate current source | Pending | Pending |
| Native Play/Pause/seek | Observe current source; pause saves observed position | Pending | Pending |
| Wired unplug / Bluetooth loss | Privacy-preserving pause request/behavior; record route separately | Pending | Pending |
| Focus or call interruption begin/end | Record native suppression/recovery; app never auto-plays on window resume | Pending | Pending |
| End/failure | Save useful checkpoint; release session; stale event ignored | Pending | Pending |
| Leave Shell lab / destroy window | Stop/release rather than retain hidden page | Pending | Pending |
| Task removal / force-stop / termination | Record native service shutdown and earlier confirmed checkpoint | Pending | Pending |
| Battery saver / long idle | Record OS restrictions; no keep-alive promise | Pending | Pending |

System next/previous queue commands, remote media URLs, artwork, private deliveries and cross-page application-level playback are not implemented. A finite sparse timing WAV is a diagnostic fixture, not a silent keep-alive mechanism.

## Automated evidence

Portable tests exercise an explicitly simulated player, including visibility policy, cancellation-ignoring preparation, stale events, native-pause observations, last confirmed save, cleanup uncertainty, actual seek results and disposal. Fixture tests inspect committed PCM headers, lengths and hashes. Android CI compiles the adapter and asserts merged service permissions/type. Neither test category proves native decoding, notification behavior, route handling or audibility.

See `verification.md` for exact executed revisions and run links. Platform references: https://developer.android.com/media/media3/session/background-playback, https://developer.android.com/media/optimize/audio-focus, https://developer.apple.com/documentation/avfaudio/responding-to-audio-route-changes.
