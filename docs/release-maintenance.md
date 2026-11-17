# Chapter 40 release rehearsal and maintenance

This is a runnable synthetic packet exercise, not a production publisher.
Implementation checkpoint: 22489d1906110ebf4925aa6b2a2b04c0b2833130.
No application C# changes. Read docs/reviews/ch40-release-review.md for the actual
local result and pending native/provider/distribution work.

## Run locally

Python 3.12; dedicated checkout. In PowerShell at the companion root:

```powershell
$codeRevision = git rev-parse HEAD
$manuscriptRevision = 'YOUR FORTY-CHARACTER MANUSCRIPT SHA'
python -m unittest discover -s eng/release-rehearsal -p test_packet.py
python eng/release-rehearsal/demo.py `
  --output artifacts/my-release-rehearsal `
  --revision $codeRevision `
  --manuscript-revision $manuscriptRevision
```

Replace the manuscript placeholder and use a new destination. Bash equivalents:

```bash
code_revision=$(git rev-parse HEAD)
manuscript_revision=YOUR_FORTY_CHARACTER_MANUSCRIPT_SHA
python3 -m unittest discover -s eng/release-rehearsal -p test_packet.py
python3 eng/release-rehearsal/demo.py \
  --output artifacts/my-release-rehearsal \
  --revision "$code_revision" \
  --manuscript-revision "$manuscript_revision"
```

Actual local observation used manuscript checkpoint
`dcd2f9b1bfe64a9a80c99beb51b8d2aa76224e56` and the implementation checkpoint above.
Later chapter and documentation changes do not relabel that execution.

The demo creates invented candidate.bin bytes (not an APK/IPA), a plan and four
synthetic receipts. It builds a bounded public-safe packet, changes actual bytes,
requires rejection, restores its original owned bytes and validates again.
Expected lines:

```text
Synthetic packet: pass; distribution: blocked.
Changed candidate bytes rejected as expected.
Restored synthetic fixture validates; all manual gates remain not_run.
```

A direct packet invocation has this form (PowerShell):

```powershell
python eng/release-rehearsal/packet.py `
  --directory artifacts/my-release-rehearsal/fixture `
  --revision $codeRevision `
  --manuscript-revision $manuscriptRevision `
  --output artifacts/my-release-rehearsal/another-packet.json
```

Exit 0 means matching synthetic declared-pass receipts; exit 1 means a valid
packet contains a declared failed receipt; exit 2 means rejected input/output.
All valid packets retain distribution blocked and release readiness unassessed.
The tool rejects duplicate keys, unknown fields, cross-candidate/swapped receipts,
missing gates, changed hashes, traversal/symlink inputs and oversized files. It
never overwrites existing output. Use a controlled owned directory; path checks
are not a sandbox against a concurrent adversary controlling the parent directory.

## Important limits

The program verifies local bytes and metadata consistency, not test authenticity.
The revisions are selected input metadata, not Git attestations. The four synthetic
receipts are invented, not actual portable/API/package/device passes. Real pipelines
need an explicit subject graph; real source tests and package checks do not all
have one artifact hash. Preserve original source/harness/run identities rather
than editing historical evidence to make it match a new candidate.

The manual-dispatch workflow release-rehearsal.yml takes a manuscript revision,
runs boundary tests and uploads only the safe synthetic packet with seven-day
retention. It requests contents read, has no signing secret and performs no release.
It remains unexecuted until the workflow is published and intentionally dispatched.
Actions currently use version tags; a verified dependency lockfile/pinning policy
remains production maintenance work.

## Actual automation map

| Workflow | Scope |
| --- | --- |
| core.yml | Windows portable compilation/tests and selected console labs |
| api.yml | API tests, probe compilation and local listener smoke |
| android.yml | Android compilation |
| acceptance.yml | Same-job suite projection; manual rows remain not_run |
| operations.yml | Published loopback HTTPS restore/hold/release drill |
| performance.yml | Scoped Release desktop investigation |
| android-release-lab.yml | Disposable signed package/audit and emulator lab |
| android-upgrade-candidate.yml | Hash-bound recheck of immutable package candidate |
| release-rehearsal.yml | Synthetic consistency/failure exercise only |

Check triggers, path filters, source SHA, actual run and artifact retention before
using evidence. A docs commit does not silently rerun the full matrix.

## Candidate record for a future real release

Record manuscript and code commits, actual resolved dependencies/workloads/runner,
package ID/build/display versions, signer public metadata, artifact hashes,
archive/symbol retention, API/schema environment, supported client compatibility
window, exact evidence links/subjects, unresolved gates and owner decision.
Do not put private keys/passwords, private device lists, databases or raw dumps in
public-safe reports. Environment protection must actually be configured; merely
naming an environment in YAML does not configure reviewers or release policy.

## Upgrade and recovery

Use the real Chapter 38 Android update record under its original candidate/harness
identity. It establishes isolated testOnly Release emulator draft recovery, not a
production store rollout. iOS and physical-device/provider acceptance remain pending.
For a new real candidate, seed confirmed invented data in version 1, install version 2
without uninstall, then reopen the same data in a new process. Keep bundle/package
identity and intended signing context consistent. Reconcile pending operations by
their original keys; do not treat a lost response as proof of an unprocessed write.

Before server/data change, define supported old-client contracts, migration failures,
backup restoration, generation review, replay safety and permission to resume. An old
binary does not reverse migrations or external side effects. Preserve affected state
and investigate a compatible forward correction when safer than restoring old data.

## Maintenance worksheet

Assign actual people to roles before a public service is promised:

| Role | Trigger | Action/evidence |
| --- | --- | --- |
| Release owner | New candidate or incident | Decide gates/rollout; retain unresolved scope |
| App maintainer | Workload, signing or platform change | Inventory tools and repeat affected native/package checks |
| API/data maintainer | Contract/schema change or recovery | Check old/new clients; restore and reconcile writes |
| Provider owner | Identity/payment requirement change | Reproduce enrollment/recovery/event boundaries |
| Support/privacy owner | Customer fault or collection change | Bounded intake, consent, retention and disposition |
| Editorial maintainer | Instruction/source correction | Update erratum and exact edition/source map |

A person may hold several roles. Keep release artifacts/symbols and support records
under deliberate access/retention policies. Scan direct/transitive dependencies,
review advisories and verify affected targets; no advisory scan is claimed here.
Create immutable chapter/edition tags only after intended checkpoints are verified;
no new public tag or release is created by this lab.

## Current primary references (November 17, 2026)

- https://docs.github.com/en/actions/reference/workflows-and-actions/deployments-and-environments
- https://docs.github.com/en/actions/concepts/security/workflow-dependency-lockfiles
- https://docs.github.com/en/actions/reference/security/secure-use
- https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list
- https://learn.microsoft.com/en-us/dotnet/maui/deployment/runtimes-compilation
