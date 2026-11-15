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
