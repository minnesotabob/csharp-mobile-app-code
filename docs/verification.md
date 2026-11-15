# Chapter 26 protected-read security - November 15, 2026 (Central)

Tested source: `a3d2a880195714dc61a2971bc2ab3f7aab1d78a9` on main.
No new package dependency: SDK 11.0.100, C# 15, MAUI 11.0.10; framework rate limiter.

- Portable Release build: 0 warnings/errors; **360 cases pass**, including 15 new
  destination/identifier-boundary cases; no errors/failures/skips/not-run cases.
  Both Chapter 21 and Chapter 25 console labs execute successfully.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43761369874
- API/probe Release builds: 0 warnings/errors; **46 API cases pass**, including nine
  new ID/cache/budget/audit cases; no errors/failures/skips/not-run cases. Real public
  loopback smoke passes for health, catalog and pagination.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43761369921
- Android Debug compilation: success; 0 final compiler warnings/errors.
  Dependency setup retains the documented unresolved cmdline-tools;21.0 warning.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43761369900

The server calls the shared 64-character ASCII request-ID policy, emits no-store for
private-prefix responses, enforces a named eight/minute aggregate process-local read
budget after authorization, and records event 2601 using fixed decision strings only.
API tests keep real bearer middleware/static ephemeral RSA metadata; no fake login
handler. They check 400, private no-store, ninth-call 429/Retry-After, public availability,
pre-budget 401/403, shared-subject budget and a minimal custom audit projection.

Limits: the limiter is not per-user fair, distributed, restart-persistent or protection
against auth/public/volumetric load. Fixed-window bursts remain possible. The log test
covers only the ReadSecurityAudit category, not framework/proxy/provider sinks or a
complete tamper-resistant audit. Device caching, OS callbacks, TLS, secure storage and
backups remain pending. Current package-advisory/secret scans are documented but not
performed by these tests. No customer-file or lifecycle endpoint is added. Live provider,
iOS/native runtime, full removal and screenshots remain pending as earlier chapters state.
Read docs/security/threat-model.md and docs/labs/ch26-security-verification.md.

## Earlier verification records

# Chapter 25 account workspace - November 15, 2026 (Central)

Tested executable source: `3309637a8abc06ecf1e65fafd1bec32afbbe315b` on main.
Toolchain remains SDK 11.0.100, C# 15, MAUI 11.0.10 and Microsoft.Data.Sqlite 11.0.0.

- Portable Release compilation: 0 warnings/errors. **345 cases passed**, no errors,
  failures, skipped or not-run cases; 24 added account-workspace cases.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43760294885
- Chapter 25 console lab and existing Chapter 21 lab executed successfully. The new
  transcript explicitly marks provider/service receipts SIMULATED. Boolean checks
  for account isolation, obsolete handles, pending-removal freeze and stable retry pass.
- API and probe Release compilation: 0 warnings/errors. **37 API cases passed**,
  no errors/failures/skips/not run; real loopback public-catalog smoke passed.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43760294883
- Android Debug compilation: success; 0 final compiler warnings/errors.
  Dependency setup retains the documented cmdline-tools;21.0 warning.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43760294896

The new tests use real temporary SQLite files. They verify restart persistence,
issuer/subject isolation, A-B-A handle fencing, consent withdrawal/re-enable, stale
editor rejection, scoped local export, durable request identity, wrong-owner receipts,
accepted/completed separation, monotonic completion and rollback after an injected
SQLite purge failure. They do not perform authentication or delete a real account.

This is a separate portable lab, not native settings/session composition. Existing
anonymous favorites, drafts and replicas are not migrated. Lifecycle transport,
server authorization/reauthentication, complete remote export/deletion, provider
removal/revocation, notification registration, iOS, device UX and screenshots remain
pending. SQLite is not app-encrypted and a deletion receipt retains owner identifiers
without an implemented expiry. Physical erasure/backups and production retention
policy are not established. See docs/labs/ch25-account-lifecycle.md.

## Earlier verification records

# Chapter 24 provider lab - November 15, 2026 (Central)

Documentation-only addition: hosted MFA configuration, recovery/replacement worksheet,
interruption/renewal observations and future step-up contract. No executable source
or package change; Chapter 23 test totals below are historical evidence, not a new
MFA result. Official provider documentation reviewed; live tenant/device execution,
recovery-code consumption, factor replacement and screenshots remain pending.

# Chapter 23 sessions and API authorization — November 15, 2026 (Central)

Tested source: `50a5abf391e37fef04866c0528af4da195954354` on `work/chapter16-pricing`.
SDK 11.0.100; C# 15; MAUI 11.0.10; Auth0 MAUI SDK 1.4.0; API adds
Microsoft.AspNetCore.Authentication.JwtBearer 11.0.0. Provider configuration remains null.

- Portable Release build: **0 warnings/errors; 321 tests passed**, no errors, failures,
  skipped or not-run cases. This includes 14 new session/client cases.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43746174414
- API/probe Release builds: **0 warnings/errors; 37 API integration tests passed**,
  no errors/failures/skips/not run, including 11 signed-token/ownership cases. Existing
  real-loopback public catalog smoke also passed.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43746174402
- Android Debug compilation: **success; 0 final compiler warnings/errors**.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43746174337
  SDK dependency setup retains the documented unresolved cmdline-tools;21.0 warning.

Session checks cover envelope restore, serialized rotation, save failure after renewal,
uncertain refresh, sign-out during renewal, incomplete persistent cleanup, corrupt vault,
changed subject, bounded preview, per-request bearer headers, rejected statuses and a
late response from an earlier session. Portable vault/provider doubles do not test native
encryption, platform restore or real Auth0 renewal.

API tests use real JWT bearer validation with ephemeral per-factory RSA signing material
and static metadata injected only through test-host DI. They reject malformed, expired,
wrong-key, wrong-issuer, wrong-audience and underscoped credentials; enforce owner lookup;
and show that an account query parameter cannot replace validated claims. The running
API seeds no customer records or signing keys. Its default unconfigured protected read
does not accept those fixture credentials. This is not a production token issuer.

The native Session lab is separate from Chapter 22's process-only Account page. It
uses one SecureStorage envelope, SDK renewal and one configured HTTPS API origin with
redirects disabled. No existing favorites/drafts are migrated. Local sign-out is not
provider logout, grant revocation or account deletion. A failed secure overwrite blocks
current-process use without claiming stored credentials were removed.

**Unverified:** live provider acquisition/rotation/revocation, actual phone secure-storage
and reinstall/backup behavior, native sign-out/cancellation UX, device-to-API TLS, iOS
build/runtime and screenshots. The default server has an empty customer store, so real
owned-record setup is also pending. Follow docs/labs/ch23-sessions-api.md.

## Earlier verification records

# Chapter 22 native identity adapter — November 15, 2026 (Central)

Corrected source: `13b32a90075de1d1bd8701237c61f404253f707d` on `work/chapter16-pricing`.
Initial implementation: `3e7ffa04e9328d52d074103d7bdfb7304d361461`.

SDK 11.0.100; C# 15; MAUI Controls 11.0.10; Auth0.OidcClient.MAUI 1.4.0,
Auth0.OidcClient.Core 4.2.0, underlying Duende.IdentityModel.OidcClient 6.0.1.
Portable tests reference the underlying engine 6.0.1 explicitly; the app uses the
provider SDK. The initial Android check found an ambiguous IBrowser name; the
corrected source fully qualifies the provider interface.

- Corrected portable Release build: **0 warnings/errors; 307 cases passed**, no
  errors, failures, skipped or not-run cases. This includes 23 new Chapter 22 cases.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43744434467
- Corrected Android Debug compilation: **success, 0 final compiler warnings/errors**.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43744434539
  Android dependency installation retains the earlier unresolved cmdline-tools;21.0
  setup warning. That warning is separate from the clean final compilation.
- Existing API regression workflow passed at initial implementation (the subsequent
  correction only disambiguates the native browser interface):
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43743986823

The tests exercise simulated presentation outcomes, destination guards, cancellation,
duplicate taps, retry, sanitized errors and late-result ownership. Three checks use
the underlying SDK engine with static metadata and a no-network handler: unrelated
callback state, missing authorization code and canceled browser. They do not mint or
validate a real provider token and do not perform a native browser round trip.

The distributed configuration is intentionally null. **Live Auth0 tenant sign-in,
provider enrollment, actual Android browser return/cancellation, iOS build/device,
MFA/recovery and native screenshots are NOT verified.** The chapter is a setup/integration
draft pending that matrix, not a completed real-provider outcome. No tenant was created,
paid service enabled or credentials requested. No token is exposed to the view model
or persisted by this adapter. Anonymous catalog behavior remains independent.

Follow docs/labs/ch22-identity.md for exact public client setup and native checks.
Session storage, provider logout/revocation, authenticated API access and associating
existing drafts/favorites with identity remain Chapter 23+ work.

## Earlier verification records

# Chapter 21 synchronization verification — November 15, 2026 (Central)

Tested source: `84a3278e5d00b64699ccf8cee963a657da9fdec8` on `work/chapter16-pricing`.
SDK 11.0.100; C# 15; Microsoft.Data.Sqlite 11.0.0; xUnit v3 4.0.0.
No dependency version was added or upgraded for the protocol lab.

- Portable Release build: 0 warnings/errors. **284 executable tests passed**, no
  errors, failures, skipped or not-run cases. This includes 18 new Chapter 21 cases.
  Push workflow: https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43737262027
- Chapter 21 console lab ran successfully in that workflow and produced the expected
  draft-conflict, explicit-resolution and favorite-deletion transcript in docs/labs/ch21-sync.md.
- Android Debug compilation: success, final compilation 0 warnings/errors.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43737262069
  Dependency installation retains the previously documented unresolved cmdline-tools;21.0 warning.
- Existing API regression suite: **26 passed**, no errors/failures/skips/not-run cases;
  API and probe builds 0 warnings/errors. No sync endpoint was added.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43737262061

Coverage includes two devices, four value kinds, retained conflicting text, tombstones,
uncertain acceptance followed by reopen/replay, cancellation after server acceptance,
account partition/file-owner guards, operation-ID misuse, bounded paging, atomic
row/cursor rollback, a local edit during download, malformed pages and worker exclusion.

The replicas use real disposable SQLite files on Windows CI; the server is an in-memory,
process-local fixture. Account labels are simulated, not authenticated. Server durability,
HTTP sync/TLS, native screen wiring, account switching in the app, Android/iOS runtime,
background jobs and production deployment remain unverified. Existing public-catalog
and saved-draft UI behavior is not replaced by the teaching replicas.

## Earlier verification records

# Chapter 20 verification — November 15, 2026 (Central)

Portable verification revision: `6553709cd7b2c770cfc6da8a8fcb2abcfa98ef7a`.

| Check | Result | Evidence |
|---|---|---|
| Portable Release build | 0 warnings, 0 errors | https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43731444673 |
| Portable executable xUnit suite | **266 passed**, 0 errors/failures/skips/not run | Same run |
| Android Debug compilation | **Passed**, runtime/core source at `74d2df5ade5b1d69b306bdb1a406722d08057ed1`; later changes only test cancellation and lab documentation | https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43731012298 |
| Native offline UI / OS job registration / iOS | **Not performed** | Pending adapters and devices |

Thirteen additional executed cases cover first-run fallback without GET, freshness
boundary and clock correction, unknown retrieval time, durable refresh intent after
reopening, classified network failure, cancellation, a newer ticket during fetch,
transaction rollback between snapshot save and acknowledgment, version-2 migration
retaining favorites/progress, and one-coordinator exclusion. The existing Chapter 19
suite continues testing actual SQLite files, including v1 migration and rejected writes.

One coordinator must own the store. No cross-process/multiworker lease, automatic
retry cooldown, native browser integration, OS scheduler or customer-order outbox is
implemented. A pending public GET is repeatable; it is not a payment or server receipt.
No physical power-loss, native SQLite loading, UI responsiveness, TLS/device or iOS
claim follows from runner tests. See `docs/labs/ch20-offline.md` for the runnable lab.

Initial Chapter 20 CI passed but reported xUnit cancellation analyzer warnings in
both SQLite test files. Test-run tokens were propagated; the recorded final portable
build is clean. Runner action Node deprecation notices remain separate from C# warnings.

## Earlier verification record

# Chapter 19 verification — November 15, 2026 (Central)

**Verified source revision:** `3f7b162dc2f9c7553b3e170ef7b44dcc59c9f26b` (review branch).

| Check | Result | Evidence |
|---|---|---|
| Windows portable Release + executable tests | **253 passed**, zero errors/failures/skips | https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43720268893 |
| Windows Android Debug compilation | **Passed** | https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43720264777 |
| ASP.NET Core API tests | Previously passed after the SQLite package addition; no API endpoint changed at the final MAUI DI commit | Earlier workflow results below |
| Android SQLite runtime and app UX | **Not tested** | Pending device/emulator |
| iOS build, storage and device runtime | **Not tested** | Pending Mac/device setup |

Chapter 19 adds `LocalCatalogDatabase` with real-file tests for fresh schema v2, v1 upgrade retaining favorites, version-99 refusal, coherent document replacement, local scope partitioning, cancellation, database-trigger failure retaining earlier progress, and concurrent writes through one store. Those checks establish real SQLite behavior on the Windows CI runner. They do not establish performance, native library loading on a phone, disk-full recovery, encryption, secure account ownership, backup policy, or sync. The SQLite store is registered but not yet used by the main browsing UI, and personal song drafts remain in their existing explicit JSON store.

## Earlier verification record

# Chapter 18 verification — November 15, 2026 (Central)

Verified source revision: 5c24e63dffd494463716730fdf553af74453a2ae.

| Check | Environment | Result |
|---|---|---|
| Portable Release compilation | GitHub-hosted Windows | Zero warnings/errors |
| Executable portable suite | GitHub-hosted Windows | 245 tests; zero errors/failures/skipped/not run |
| API/test Release compilation | GitHub-hosted Windows | Zero warnings/errors |
| Executable API integration suite | GitHub-hosted Windows, TestServer | 26 tests; zero errors/failures/skipped/not run |
| HTTPS catalog probe compilation | GitHub-hosted Windows | Success; zero warnings/errors |
| Kestrel startup and health/catalog/page smoke | Real temporary loopback HTTP listener | Pass; process stopped afterward |
| Android Debug compilation | GitHub-hosted Windows | Success; final compile zero warnings/errors |
| Real HTTPS probe/device connection / iOS build | Not performed | Pending |

Portable: https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43709057778
API/tests/probe/smoke: https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43709057781
Android: https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43709057777

Ten new portable document cases extend the earlier 235 tests. The 26 API cases
exercise actual routing, DI, JSON/DTO serialization, client/server compatibility,
pagination and revision conflicts, malformed/duplicate queries, safe errors,
development OpenAPI and absence of customer writes. TestServer does not use sockets
or establish TLS/device connectivity. The separate Kestrel smoke uses loopback HTTP
only; the compiled HTTPS probe has not been run against a real trusted endpoint here.

The new BoundedJsonClient factors the existing Chapter 17 GET transport without
changing its time, retry or byte budgets. The complete document validates creator
relationships; the compatibility array remains available. No public deployment,
database, account, tunnel, customer-data mutation or actual payment is added.
Main app browsing and native networking lab remain bundled/in-process respectively.

Android setup retains the historical cmdline-tools warning, and runner actions
report existing runtime deprecations. Final compilation is clean. Native TLS,
redirect/addressing, device UX and iOS compilation remain pending.

Changes continue in existing draft PR #1; default branches are unchanged. Follow-up
commits update documentation and quote the PowerShell project path for folders with spaces.
API/mobile source and test cases remain identical to the verified revision. No new verified chapter tag is created.

## Earlier verification record

# Chapter 17 verification — November 15, 2026 (Central)

Verified source revision: 352b0bc901b6de7b54494922e181e6311edf34b1.

| Check | Environment | Result |
|---|---|---|
| Portable Release compilation | GitHub-hosted Windows | Zero warnings/errors |
| Executable xUnit suite | GitHub-hosted Windows | 235 tests; zero errors/failures/skipped/not run |
| Android Debug compilation | GitHub-hosted Windows | Success; final compile zero warnings/errors |
| Native device execution / real socket transport | Not performed | Pending |
| iOS compilation and runtime | Not performed | Pending |

Portable evidence: https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43706546704
Android evidence: https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43706546699

Thirty-seven new cases exercise request shape, fixture data, malformed JSON/metadata,
media types, permanent/transient statuses, bounded retries, Retry-After delta/date,
response disposal before waits, classified transport failures, payload sizes,
caller cancellation and deadline cancellation during body reads. Time/delays and
transport are controlled; no public service is used by the suite or native lab.

The native lab uses an explicit in-process handler and shows a connectivity hint.
It does not contact a live API, replace the main bundled catalog, test internet
access, persist an offline cache, or authorize customer workflows. Local-address,
TLS/redirect, socket-interruption and actual Android/iOS observations remain separate.
New lab strings remain English pending localization/focus refinement.

The items-only HTTP contract validates CatalogLoader metadata but not relationships
against a separately refreshed creators list. Chapter 18 must establish the real
service schema/integration before moving the main browser onto remote data.

Android dependency setup retains the historical cmdline-tools;21.0 warning. The
final compile is clean. Platform build setup also reports existing action/runtime
deprecations. These are environment maintenance items, not failed source checks.

All work remains in draft PR #1 on work/chapter16-pricing. Default branches are
unchanged. The follow-up commit only updates documentation; no code tag is created.

## Earlier verification record

# Chapter 16 verification — November 15, 2026 (Central)

Verified portable source revision: 2d15aa9452ef2f70b66176c99aebb3acf8027362.

- Windows Release portable compilation: zero warnings and zero errors.
- Executable xUnit suite: 198 tests; zero errors, failures, skipped or not run.
- Portable evidence: https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43704723565
- Includes 27 recovered Chapter 14 cases, 29 Chapter 15 cases and 30 Chapter 16 cases
  in addition to the historical 112-case baseline.
- Android Debug compilation on Windows: SUCCESS, final compilation zero warnings/errors.
- Android evidence: https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43704723559
- Android dependency setup retains the historical unresolved cmdline-tools;21.0 warning;
  it did not prevent the final successful compilation.
- Actual Android/iOS device execution and iOS compilation: not performed.

The initial native compilation exposed unsupported SetBinding named-argument
overloads in the restored form page. Calls now construct explicit Binding objects.
The earlier portable run passed but reported xUnit1051/xUnit2031 warnings; tests
now pass cancellation tokens and use the filtering Assert.Single overload.
The final portable compilation is clean.

Chapter 16 covers archived pricing for one to three songs, canonical quote
snapshots, exact expiry boundaries, changed-request rejection, key conflicts,
quote single use, atomic in-process acceptance, client overlap protection and
same-operation retry when a response is lost after acceptance. No live orders,
email, payment or customer transmission are implemented. All quote/idempotency
state is process-local and disappears on restart. Account-scoped durable recovery,
HTTP/service authorization, form localization and focus refinement remain pending.

Changes are available on work/chapter16-pricing and draft PR #1. Default-branch
publication was not retried. No chapter milestone tag or native runtime claim.

## Historical draft records (superseded by the evidence above)

# Chapter 15 draft verification — November 15, 2026 (Central)

Adds SongRequest rules, a three-step form, conditional vocal notes, explicit local
save/discard, a process-shared editing model and review invalidation. The anonymous
local JSON adapter is not account-scoped or app-encrypted. Form localization,
field-level accessibility focus and recovery export remain follow-up work.

29 new portable cases are authored in SongRequestTests.cs, not executed. There is
no dotnet executable in this continuation environment. Portable build/test, Android
build and all device/iOS checks remain PENDING for both recovered Chapter 14 source
and Chapter 15 additions. XML/resource/JSON syntax and diff whitespace pass. The
previous default-branch push rejection was not retried. No CI/publication claim.

Run from the repository root with the pinned supported Windows toolchain:

```powershell
$tests = "tests/GnazzosWorld.Core.Tests/GnazzosWorld.Core.Tests.csproj"
dotnet build $tests -c Release
dotnet run --project $tests -c Release --no-build
```

Then build the Android target with the configured workload, and complete the
Chapter 15 scenarios in device-checks.md. Do not infer native execution from tests.

## Earlier milestone record

# Chapter 14 draft verification — November 15, 2026 (Central)

Chapter 14 source adds detail metadata, an explicitly labeled original reading fixture,
reviewed HTTPS destinations, a native external-browser adapter and controlled completion state.
27 new portable cases are written in DetailJourneyTests.cs. XML, unique resource keys,
JSON syntax and git diff whitespace checks pass. Compilation and all new test execution
are PENDING. Automatic review blocked the default-branch push; no Chapter 14 CI run exists.
Do not use the older CI results below as proof of the new source.

Device checks: website handoff/return on phone and inline tablet details, unavailable/missing
records, long sample text at large text sizes, Spanish, focus/announcement and interruptions.
The native browser API cannot report all user cancel/close behavior. Local canceled-adapter
cases verify only the portable contract. iOS and actual Android launch remain unverified.

## Historical verification

# Chapter 13 verification — November 14, 2026 (Central)

Verified application and test source: 51870ac298eff6198e098a063bdc391fecc665f8.

| Check | Environment | Result |
|---|---|---|
| Portable Release compilation | GitHub-hosted Windows | 0 warnings/errors |
| Executable xUnit suite | GitHub-hosted Windows | 112 tests; 0 errors/failures/skipped/not run |
| Android Debug compilation | GitHub-hosted Windows | Success; final compilation 0 warnings/errors |
| Device launch and native preferences restart | Not performed | Pending |
| iOS compilation and native behavior | Not performed | Pending |

Portable run: https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43678204364
Android run: https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43678204305

Chapter 13 adds 29 portable cases. Search intersects query, content kind, ordinal creator ID,
sort mode and favorites; missing dates sort last. The page has debounced typing with
latest-input ownership, collapsible filters and favorite actions. PreferencesFavoriteStore
serializes item IDs through a bounded codec. Failed saves preserve visible state; failed
initial reads disable edits while allowing ordinary browsing. Reload favorites retries a read.

The portable store-recreation test does not prove actual platform persistence. Page lifecycle,
keyboard behavior, expanded-filter reachability, screen readers, selection restoration and
large text need native checks. iOS is still unbuilt. Remote search/result publication,
favorites synchronization, account ownership, multiwindow notifications, migrations and
corruption repair are not implemented. The preferences format is limited to 32 IDs,
128 characters per ID and 4096 serialized UTF-8 bytes.

Android dependency setup retains the previously documented cmdline-tools warning; the final
compilation is clean. Earlier source milestones and toolchain history follow below.

## Earlier verification record

# Chapter 12 verification — November 14, 2026 (Central)

Portable source revision: cda4092131cfbf9793587fc36a2c5bba67bfd52d.

- Windows Release build: 0 warnings/errors.
- Executable xUnit suite: 83 tests, 0 errors/failures/skipped/not run.
- Evidence: https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43676066332
- Android Debug build passed at d496a51b86d1bb9b123745eecf58379695e62bbc:
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43675861183
- The later revision changes only a test assertion to remove xUnit2031; app/core source is identical.
- Device execution and iOS compilation remain unverified.

The 22 added cases exercise optional metadata, invalid values, creator relationships,
stream ownership and bundled snapshot consistency. The five existing items retain stable IDs.
New metadata is not wired into native UI labels yet. Unknown release dates/advisories remain
unknown. Creator display strings remain compatibility fields and must be refreshed explicitly
after a creator rename. This is a bundled import contract, not a live provider integration.

## Earlier Part II verification record

# Parts I and II verification — November 14, 2026 (Central)

Verified implementation: 9497b0d7e8acb74c07c50be8cb56f26453664fbb.

| Check | Environment | Result |
|---|---|---|
| Portable Release build and executable xUnit suite | GitHub-hosted Windows | 61 tests; 0 errors/failures/skipped/not run |
| Android Debug compilation | GitHub-hosted Windows, isolated SDK | Success, final compile 0 warnings/errors |
| Device/emulator execution | Not performed | Pending |
| iOS build and native behavior | Not performed | Pending |

Portable run: https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43674393990  
Android run: https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43674393959

## Coverage and limits

The app adds Shell Home/Discover routes, details passed by validated stable ID, typed XAML bindings, a portable catalog browser view model, search/kind filters, reusable cards/detail view, theme resources, English/Spanish resources and inline details at widths of at least 900 device-independent units.

Tests cover search/filter/selection consistency, stale detail clearing, route payload rejection, navigation errors, command overlap/recovery, notifications, width boundaries/nonfinite widths, resource fallback and singular result labels. The original model/loader/search/pricing cases remain. These are portable behavior tests; native layouts, focus, screen readers and platform bindings require the pending device checks.

## Recorded Android toolchain

- SDK/workload set 11.0.100; runtime 11.0.0.
- maui-android manifest 11.0.0/11.0.100.
- Android workload manifest/pack 37.2.29.
- Microsoft.Maui.Controls 11.0.10.
- Microsoft OpenJDK 21.0.11+10 LTS.
- net11.0-android Debug build.
- SDK installed under runner.temp, avoiding preinstalled preview workloads.

Dependency installation emitted one unresolved cmdline-tools;21.0 warning. The final Android compile had zero warnings/errors. The setup warning remains an environment cleanup item.

## Historical baseline

Part I source is preserved at 0b8973660cc21269ff79eb54733edaf428fb5dac; see milestones.md. Earlier Part I Windows CI passed 37 tests and Android compilation. The isolated SDK corrected an initial stable/preview Java.Interop version mismatch; the current expanded source also compiles.

Prior local workload/test attempts were blocked by automatic review after attempted cloud metadata access. They were not retried. The separate GitHub-hosted Windows runs above provide the stated evidence.

## Pending

Emulator/hardware launch, iOS compilation/Xcode/signing, native accessibility, keyboard/rotation/lifecycle, device language changes, runtime theme rendering and Release-device performance. Global Shell navigation is for the active single-window flow; full multiwindow ownership is not implemented. Remote images, backend, identity/MFA, media, payments and deployment remain later work. Artwork in the manuscript is editorial illustration, never runtime evidence.
