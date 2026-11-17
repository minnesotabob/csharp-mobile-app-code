# Chapter 40 local synthetic rehearsal — November 17, 2026 (Central)

Executed implementation: 22489d1906110ebf4925aa6b2a2b04c0b2833130.
Selected manuscript metadata: dcd2f9b1bfe64a9a80c99beb51b8d2aa76224e56.
The manuscript checkpoint precedes Chapter 40; it is retained as the actual input,
not silently rewritten to a future chapter commit. Later docs preserve this identity.

Environment: Linux, Python 3.12.14. The source supports the documented Python 3.12
workflow, but that GitHub job has not executed. Local interpreter version is recorded
from the actual environment; see planning/progress.md for any corrected inventory.

Actual commands: unittest discovery under eng/release-rehearsal and demo.py with
new output artifacts/ch40-verified plus the exact revisions above. All 17 boundary
tests pass, none skipped. Synthetic packet validates; mutation of actual invented
candidate bytes is rejected; restoration validates again. The new packet is retained
as ch40-synthetic-packet.json without changing its original source metadata.

Candidate fixture SHA-256:
74128b381e8e63187495a270377db37c43492c7c776c122d15a08340d793e24f.
These bytes explicitly identify themselves as invented, not an APK or IPA.
The four declared receipt passes are synthetic. Manual gates remain not_run,
distribution blocked and release_readiness unassessed.

Checks exercise changed hashes, stale code/manuscript, cross-candidate receipts,
swapped/missing gates, failed/invalid statuses, duplicate JSON keys, unknown fields,
traversal/absolute inputs, symlinks, size bounds, missing/malformed files, existing
output, boolean schema versions and attempted production profile claims.

No application C# changed and no fresh .NET suites, Android build/emulator, API,
iOS, live-provider or store observation is implied. Existing signed update and
HTTPS restore evidence retains its original subjects. Workflow publication and
GitHub execution remain pending after automatic approval review blocked main pushes.
The new manual-dispatch workflow has no signing secrets or deployment operation.
