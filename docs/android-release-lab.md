# Chapter 38: disposable Android Release rehearsal

This is a signing, package-inspection and emulator-upgrade exercise. It does not use
publisher credentials, upload to Google Play or make the teaching application ready
for customer orders. The ordinary app remains separate from this test-only profile.

## Start here

Use the exact executed source in docs/verification.md. On Windows use SDK/workload
11.0.100, the documented Android SDK and Microsoft JDK 21, plus Python 3.12.
Read `.github/workflows/android-release-lab.yml` for the full clean-run setup.

```powershell
python -m unittest discover -s eng/android-release-lab -p test_audit.py
./eng/android-release-lab/Build-ReleaseLab.ps1 -Output artifacts/android-release-lab
```

The output directory must not exist. The full script creates one disposable PKCS12
key/certificate, uses separate password files, publishes v1/v2 x86-64 APKs and v2 arm64
AAB, and removes the owned private material in finally. It passes distinct per-variant manifests with explicit version code/name as project-relative paths, preventing stale manifest reuse across the upgrade versions. The certificate lasts two days; do not reuse
this retention policy for a real publisher key.

The profile requires Release, net11.0-android, explicit signing and isolated ID
`com.johngnazzo.gnazzosworld.releaselab`. Its generated manifest is testOnly. The
conditional exported probe checks the ID and testOnly flag again at runtime. Never
copy this probe into a production profile. No real customer data belongs here.

## Package audit

Get pinned official bundletool 1.18.3 and verify the SHA-256 in the workflow. Invoke
`eng/android-release-lab/audit.py` with --directory, --sdk, --java, --bundletool,
--revision (actual 40-character source SHA) and --run-id (actual numeric run ID).
The workflow shows the complete PowerShell command and dependency setup.

The audit checks APK signatures/certificate identity, package ID/version/SDK values,
testOnly probe, APK ZIP alignment, supported ELF load-segment alignment, and the
AAB signature/certificate/manifest/bundle structure. It emits package-evidence.json
only after all checks pass. A report is not a signed attestation: source/run values
must be tied to the actual workflow and artifact association.

The apksigner parser handles both numbered and signature-scheme labels (including
observed `V3.0 Signer:`). It requires one signer and all reported certificate SHA-256
values to match the expected exported certificate. Public-key digests cannot satisfy
this check. The metadata parser accepts current minSdkVersion and older sdkVersion labels while requiring exactly one minimum. Twelve Python regression tests cover parser/manifest/ELF boundaries; these
are separate from the C# suite totals.

## Real emulator installation and update

Start exactly one fresh x86-64 emulator, API 26 or later, with adb available. The
workflow uses API 35 Google APIs/Pixel 6. Run:

```powershell
python eng/android-release-lab/smoke.py --directory artifacts/android-release-lab
```

The script refuses a non-emulator or an existing lab-package installation. Its freshness query uses pm list packages: unlike pm path, an absent package is an empty successful result. A real command failure still stops the run. It checks
source APK hashes, installs v1 with -t, resolves and starts the explicit MAIN/LAUNCHER component, observes Home, saves a synthetic draft using
the real FileRequestDraftStore, stops the process, and installs v2 with -r -t.
It requires the same UID/version 2, relaunches Home, then verifies the saved draft
from a new probe instance. Finally it uninstalls only this disposable package.

Do not uninstall between versions to fix an update problem: that discards the
storage boundary under test. The invented email is release@example.test; no provider
sign-in, payment, API transmission or store account is used.

Read upgrade-evidence.json for actual environment/page size/results. A 4096-byte
runtime retains runtime16k=not_run even after static 16 KiB alignment checks pass.
The rehearsal does not establish full schema migration, physical-device UX, media,
MFA, TalkBack, native profiling, Play-generated APKs or public release readiness.

## Production transition and review worksheet

Use a separate reviewed build profile with the intended package ID, maintained
upload key, eligible version code, actual supported ABIs and no testOnly/probe.
Verify its Release runtime/dependencies, final manifest and certificate; test the
Play-installed candidate and its service registrations independently.

Before submission record:

- Source, dependencies, target/minimum SDK, versions, digest, certificate identities
  and the actual API/schema/provider environment.
- Real screenshots, listing/support/privacy URLs and content/audience/ads/service
  declarations matching actual behavior.
- Data inventory including SDKs, transfers, retention, deletion and accurate Data
  safety answers; implement provider/application deletion rather than citing a lab.
- Tested reviewer access compatible with MFA, without global authorization bypass.
- Payment-policy assessment for actual content, purchase path and markets.
- Named Play test-track installations/updates, relevant personal-account closed-test
  eligibility, physical-device/accessibility scenarios and observed failures.
- Rollout monitoring owner/stop criteria and a forward recovery plan. Halting rollout
  does not downgrade installations or undo migrated data.

Official references, checked November 16, 2026:

- https://developer.android.com/studio/publish/app-signing
- https://developer.android.com/google/play/requirements/target-sdk
- https://developer.android.com/guide/practices/page-sizes
- https://learn.microsoft.com/dotnet/maui/android/deployment/publish-cli
- https://support.google.com/googleplay/android-developer/answer/9859455
- https://support.google.com/googleplay/android-developer/answer/10787469
- https://support.google.com/googleplay/android-developer/answer/13327111
- https://support.google.com/googleplay/android-developer/answer/14151465

Current target guidance states API 36 for new phone/tablet apps and updates from
August 31, 2026. Page-size guidance identifies a February 1, 2027 update restriction.
Recheck the current pages and account-specific Console findings before submission.
No artifact in this tutorial has been submitted to Google Play.

## Recheck an already verified immutable candidate

The optional `android-upgrade-candidate.yml` workflow downloads the public disposable
artifact identified by `eng/android-release-lab/candidate.json` using the automatic
read-only Actions token. It checks the original package report's revision/run against
that frozen identity before invoking the current smoke harness. The harness rechecks
APK hashes before installing. Application compilation and test-tool revisions are
separate evidence: upgrade-evidence.json retains the package `revision`/`runId` and
adds `harnessRevision`/`harnessRunId` for the actual test run. Do not rewrite original
package provenance to pretend the artifact was rebuilt by a newer test script.

This is a short-lived diagnostic fixture. Artifacts expire after seven days. When
unavailable, run the full signing workflow and update the frozen record only after
its package audit passes; do not silently substitute the latest artifact or remove
the identity/hash checks. The full workflow still rebuilds candidates when app,
publishing or audit source changes; its ordinary upgrade job uses the same run's
fresh artifacts.

On failure, the harness retains bounded adb output and crash-buffer diagnostics in
the CI console for this fresh synthetic emulator. No real provider/customer data is
permitted; these diagnostics are not uploaded as safe evidence. Only a completed
bounded upgrade JSON is uploaded. Actual failures remain failures, not missing/green
rows. The package job passing does not mark the upgrade or release readiness passed.
