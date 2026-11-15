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
