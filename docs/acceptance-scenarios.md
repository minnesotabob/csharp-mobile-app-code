# Acceptance scenario entry point

The current whole-application regression spine and separate manual/device/provider
matrix are in [complete-application-testing.md](complete-application-testing.md).
Run the Chapter 36 evidence workflow and inspect its generated not_run rows; it does
not certify release readiness. Exact executed source/results are in verification.md.

The Part I inventory below is historical and deliberately narrower than current source.
Its future-feature paragraph describes that milestone, not the current implementation.

# Part I acceptance scenarios

## Portable automated checks
- Bundled fixture has five unique records across Book, Song, and Podcast.
- BERET with case/whitespace variation matches Béret violet.
- Creator matching and kind filter combine; unknown query returns no items.
- Equal titles have deterministic ID tie-breaking.
- Missing required JSON properties, malformed JSON, invalid metadata, duplicate IDs, and insecure links are rejected.
- A valid empty catalog is allowed; caller-owned streams remain open.
- All twelve snapshot base prices and per-song discounts are checked; unsupported lengths/quantities fail.

## Android compilation
The Android workflow records SDK/workload/JDK and compiles the starter on a Windows runner. Compilation does not establish launch or accessibility behavior.

## Manual device checks, not yet completed
- Launch offline; inspect all five fixture records; search/clear/no-match.
- Rotate and increase text size; inspect keyboard occlusion.
- Navigate with TalkBack, then with VoiceOver after iOS builds exist.
- Compare foreground return with process restart; record exact device/build.

## Future features, not implemented by this milestone
Account isolation/MFA recovery, draft persistence, duplicate-order protection, playback interruptions, verified sandbox payment events, and private delivery each need their own acceptance evidence. Do not label their conceptual scenarios as completed tests.
