# Chapter 36: traceable acceptance across the application

Start from the exact Chapter 36 source in verification.md. SDK 11.0.100 and xUnit.v3
4.0.0 remain pinned. The report tool uses Python 3.12 standard library only. Native
workloads, identity tenants and payment accounts are not required for this automated lab.

## Run the suites and project evidence

Use a fresh output directory from the repository root. Run one command at a time and
stop after any failure. PowerShell:

```powershell
python -m unittest discover -s eng/acceptance -p test_report.py
New-Item -ItemType Directory artifacts/acceptance-private -ErrorAction Stop
dotnet build tests/GnazzosWorld.Core.Tests -c Release
dotnet build tests/GnazzosWorld.Api.Tests -c Release
dotnet run --project tests/GnazzosWorld.Core.Tests -c Release --no-build -- -xml artifacts/acceptance-private/core.xml
dotnet run --project tests/GnazzosWorld.Api.Tests -c Release --no-build -- -xml artifacts/acceptance-private/api.xml
$testedRevision = git rev-parse HEAD
python eng/acceptance/report.py --plan eng/acceptance/plan.json --core artifacts/acceptance-private/core.xml --api artifacts/acceptance-private/api.xml --revision $testedRevision --run-id 0 --output artifacts/acceptance-report.json
```

Run-id 0 denotes a local observation, not a GitHub run. Stop after a failing command;
do not reuse a previous XML file to supply a green result. Use a clean checkout and
fresh directory for each candidate, then associate results with that actual build.
The workflow uses its checked-out github.sha and run ID and creates all inputs within
one job. This binding depends on the trusted workflow; XML hashes do not authenticate
an arbitrary downloaded report or prove its source revision.

The expected success message is:

```
Acceptance automated gate: pass; manual checks: not_run; release readiness: unassessed.
```

Report statuses describe only the selected automated requirement methods and the
complete two-suite result. An unmapped failure still fails the automated gate. A renamed
or absent method, changed theory-row count, Skip or NotRun is not acceptance. A malformed
input, duplicate per-run case ID or environmental runner error produces exit 2 and no new acceptance claim. An
unsatisfied valid report produces exit 1. The tool never overwrites an existing output.
Display names need not be unique, and XML type/method metadata is optional. The reporter
uses per-run IDs for duplicate detection; absent metadata cannot match a required selector.
It always leaves manual rows not_run and release_readiness unassessed. There is no
flag for turning a compilation or a manual assertion into release approval.

## Source and executable boundary

AcceptanceJourneyTests.Anniversary_journey_recovers_committed_reply_loss has two theory
rows: acceptance-reply loss and revision-reply loss. Each uses a unique temporary root,
real SqliteOrderStore and client journals, the actual OrderApiClient/TrackingApiClient,
real signed bearer validation with an ephemeral fixture key and the actual ASP.NET
pipeline through TestServer. It does not open a real socket or launch a MAUI page.

1. Quote the invented anniversary request through the actual client.
2. Send acceptance after saving the client journal. In the acceptance row, the injected
   handler waits for a successful real endpoint response, disposes it and throws. The
   server has one committed order while the client has no confirmed receipt.
3. Reopen the client journal and reconstruct the client. Resume under the saved key;
   assert the same order/key and exactly one acceptance/history entry.
4. Use a checkout-provider double, deliver the same exact-byte signed TEST event twice
   through the webhook pipeline and assert paid state. The provider is not Stripe.
5. Advance production/review through trusted local fixture methods. Submit one revision.
   In the revision row, discard its committed response. Advance review before replay,
   reopen its journal and assert the original applied-version receipt and one revision.
6. Publish synthetic delivery, dispose the host/store and reopen the server file under a
   new host/session. Check six coherent history versions, original operation identity,
   revision and delivered state. Fetch the complete original WAV via the actual client
   grant/content routes and verify length/hash.
7. Use another valid fixture subject: owned tracking returns 404 and its local journal
   partition is empty. The original account's confirmed operation remains intact.

The assertion that the fault actually fired is essential. A path typo must not turn
this into an unnoticed happy-path test. Advancing review before revision replay proves
replay ordering across a changed version. Reopening storage proves more than reading the
same in-memory receipt, but it is not Android process death or SQLite native loading on iOS.
The business fixture uses current UTC for short-lived JWT/payment/grant checks; cases
control sequence and data without arbitrary sleeps. Time-bound unit tests separately
control their clocks. Do not advance a process-global clock shared with parallel tests.

## Requirement map

The versioned plan is a deliberately small regression spine, not an inventory of every
book requirement or a coverage percentage. Full suite results are also required clean.

| ID | Automated evidence | Remaining observation |
| --- | --- | --- |
| A36-ORDER-RECOVERY | Two composed client/API lost-reply rows | Native route/UI, account switch and lifecycle |
| A36-AUTHORIZATION | Five invalid-token bearer rows | Live provider login, MFA/recovery and tenant settings |
| A36-SYNC-REPLAY | Durable SQLite replica reopen/replay | OS termination, actual networking and device scheduling |
| A36-MEDIA-RESUME | Downloader reopen with exact Range/validator | OS storage pressure, decoding and listening |
| A36-RESTORE-REVIEW | Grant invalidation and expected-generation release | Full external reconciliation and real cutover |
| A36-DIAGNOSTIC-PRIVACY | Structured completion/trace field allowlist | Actual remote exporter/native fatal-crash payload |

The independent Chapter 35 operations workflow supplies actual published-process TLS
and recovery evidence. Its result is not silently imported into this report. Link its
same-source run separately. Android compilation, native execution, provider tests and
PDF rendering also have separate meanings and run identities.

## Manual/device/provider record

The six manual IDs in plan.json are always generated as not_run. Keep observations in
a separate protected run record. Use docs/device-checks.md and the earlier feature
worksheets. A minimal record contains scenario ID, exact code/build artifact hash,
platform/OS/device model, app/API/schema versions, clean-install or upgrade state,
locale/text scale/assistive mode, network transition, synthetic account role, timestamp,
steps, expected/actual result, evidence reference, defect link and tester. Record pass,
fail, blocked or not_run honestly; blocked is not pass. Never put passwords, recovery
codes, tokens, grant headers or customer stories into a report/screenshot.

| Manual ID | Required environment / scenario |
| --- | --- |
| A36-NATIVE-JOURNEY | Named phone and tablet: request, uncertainty/restart, owned tracking, revision, route departure and account switch |
| A36-ACCESSIBILITY | TalkBack/VoiceOver, keyboard, large text, focus, status announcements, error association and narrow/wide layout |
| A36-OS-OFFLINE-RECOVERY | Controlled offline/online transition, background/foreground and actual process termination; preserve same operation |
| A36-LIVE-MFA-RECOVERY | Isolated test tenant/accounts: browser callback, MFA enrollment/challenge/recovery and session invalidation |
| A36-HOSTED-TEST-PAYMENT | Provider test account/hosted checkout/verified redelivery; no real money; lifecycle/eligibility reviewed separately |
| A36-IOS-RELEASE | Named Mac/Xcode/device/simulator and signed candidate; compile/install/upgrade and native capability checks |

Plan a risk-based matrix: one named Android phone, Android tablet/emulator at the
layout threshold, iPhone and iPad; then supported OS extremes, text expansion/locales,
reduced motion, keyboard and screen readers. These are proposed slots, not observations.
Every slot needs a real model/OS/build before a result is recorded. Start with the
highest-risk ownership/recovery scenarios rather than exhaustively multiplying every
axis. Emulator results cannot substitute for background audio, biometric/provider,
battery or performance evidence on a physical device.

For UI automation, select stable AutomationId values and observable conditions instead
of localized label text or fixed sleeps. Use a platform-specific Appium project and
actual deployed candidate; pin/verify driver compatibility on that host. No Appium
package, driver, session or successful UI run is added by this chapter. Current official
MAUI guidance uses UIAutomator2 for Android and XCUITest on macOS for iOS. Semantic
labels and manual assistive-technology review remain separate from test selectors.

## CI artifacts, failure handling and isolation

acceptance.yml builds and runs both suites in a fresh Windows job. It validates the
reporter first, then consumes that job's XML, projects allowlisted fields and uploads
only acceptance-report.json for seven days. Raw XML may include exception text, captured
output, machine paths and test parameters. It stays in the ephemeral job workspace.
Only synthetic fixtures belong in these public test suites. Failed workflow logs also
need care; the JSON projection cannot sanitize a runner's separate console output.

The report includes static requirement/method selectors, bounded status/counts, source
revision, run ID and XML SHA-256. It excludes raw display names, messages, stack traces,
paths and arbitrary output. These hashes bind the projection to its inputs for an
operator who already holds them; they are not signatures or release attestations.

Fix a deterministic failure before retrying. Retain the original failure/defect link,
then record the corrected revision and complete relevant gate. Do not retry assertions
until lucky or replace a failed row with a later screenshot. An external environment
failure may justify a bounded retry with the original outcome retained. Quarantine
must have an owner, reason, issue, expiry and a visible acceptance gap; the current
report fails skipped/not-run rows rather than treating quarantine as completion.

Use independent temp roots per test and dispose clients, responses, journals, hosts
and stores before cleanup. Do not share a process-global current account, mutable
fixture, port or clock across parallel tests. Live integration accounts need their own
isolation and cleanup contract; do not delete somebody else's data as a test teardown.

## Troubleshooting

| Symptom | Check | Corrective action |
| --- | --- | --- |
| Runner rejects XML option | Confirm pinned xunit.v3 4.0.0 executable mode and argument separator | Use documented -xml after --; do not mix VSTest switches |
| Requirement is not_satisfied with green suite | Compare exact type/method selector and expected theory-row count | Review renamed/removed behavior and update the plan deliberately |
| Report input rejected | Schema, environmental errors, duplicate identity and summary totals | Inspect raw results locally; do not change counts to manufacture agreement |
| New run finds existing files | Directory/output are intentionally exclusive | Choose a new workspace; retain old evidence under its original revision |
| UI test passes but user cannot navigate | AutomationId only locates an element | Test semantic names, focus, announcements and real assistive interaction |
| Recovery row passes without uncertainty | Confirm DropCommittedReply.Dropped and absent client receipt | Place fault after successful real commit, not before sending |
| A green report is called release approval | Inspect manual rows and release_readiness | Complete named device/provider/release records and evaluate remaining defects |

References checked November 16, 2026: xUnit XML format https://xunit.net/docs/format-xml-v2;
xUnit.v3 4.0.0 executable switch compatibility https://xunit.net/releases/v3/4.0.0;
ASP.NET integration testing https://learn.microsoft.com/en-us/aspnet/core/testing/integration-testing;
official MAUI UI testing source https://github.com/dotnet/docs-maui/blob/main/docs/deployment/ui-testing.md.
