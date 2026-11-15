# Source milestones

## Part I foundation
Use commit 0b8973660cc21269ff79eb54733edaf428fb5dac to follow Chapters 1–5. It includes the first MainPage, NavigationPage startup, 37 passing portable tests, and recorded Android build evidence.

https://github.com/minnesotabob/csharp-mobile-app-code/tree/0b8973660cc21269ff79eb54733edaf428fb5dac

## Part II interface
The following commits develop Shell, portable view models, reusable views, localization, and width-based list/detail presentation. See docs/verification.md for the exact verified revision. Part I source remains available in history; current main intentionally has different startup/navigation.

No app-store release, runtime device validation, or chapter tag is implied by these source milestones.

Part II verified implementation: 9497b0d7e8acb74c07c50be8cb56f26453664fbb. Windows CI passed 61 portable tests and Android Debug compilation; native device and iOS verification remain pending. See verification.md for exact run links.

## Chapter 13

- Source: 51870ac298eff6198e098a063bdc391fecc665f8.
- Portable suite: 112 passing tests; Android Debug build passed.
- Adds combined discovery criteria, controlled input debounce and bounded device-local favorites.
- See verification.md for CI links; native execution and iOS remain pending.

## Chapters 14–15 draft continuation

Recovered Chapter 14 changes and Chapter 15 request form are packaged for review.
Chapter 14 adds 27 test cases; Chapter 15 adds 29. Execution/compilation of these
new cases and native checks are pending; Chapter 13 is still the verified baseline.
See verification.md for the exact distinction and device-checks.md for scenarios.

## Chapter 16 draft

Quantity, versioned archived pricing, short-lived immutable quotes, simulated
acceptance, key/payload conflicts, quote single use, in-process atomic deduplication,
client overlap protection and same-operation retry after an uncertain outcome.
Chapter 14–15 review-bundle source is recovered in the same review branch.
Default branches remain unchanged; no milestone tag is created before verification.

## Chapter 17 — bounded public-catalog HTTP reads

Verified source 352b0bc901b6de7b54494922e181e6311edf34b1 adds ICatalogSource,
HttpCatalogSource, an in-process fixture source and native lab route. All 235
portable cases pass; Android Debug compilation succeeds. See verification.md.
No live backend, actual device run, iOS build or main-catalog remote replacement.

## Chapter 18 — executable public fixture API

Source 5c24e63dffd494463716730fdf553af74453a2ae passed 245 portable cases,
26 API integration cases, an actual Kestrel loopback HTTP smoke, Android Debug
compilation and HTTPS probe compilation. No device HTTPS or iOS result is claimed.
The service owns public fixtures only; customer write routes are absent.
See verification.md and local-api.md for evidence and reader setup.

## Chapter 19 — local SQLite teaching store

Review branch `work/chapter16-pricing` adds a SQLite 11.0.0 package, public catalog snapshot storage, local favorite IDs, reading progress and transactional `user_version` migrations from 0 or 1 to 2. The MAUI composition root registers the store but does not switch the current UI to SQLite. Anonymous scope strings are local partitions, not authenticated accounts. Portable Windows CI at source revision `3f7b162dc2f9c7553b3e170ef7b44dcc59c9f26b` passed 253 cases, including eight new SQLite real-file cases. Android Debug compilation passed at the same code revision. Native/device execution and iOS remain pending. See `docs/verification.md`.

## Chapter 21 — portable synchronization lab

Source `84a3278e5d00b64699ccf8cee963a657da9fdec8` adds two SQLite replicas and an
in-memory simulated-user server. Run samples/GnazzosWorld.SyncLab using docs/labs/ch21-sync.md.
The executed suite totals 284 portable cases and the console scenario passes; see
verification.md for exact CI evidence. No native or authenticated sync is implemented.

## Chapter 22 — native provider adapter (live validation pending)

Use corrected source `13b32a90075de1d1bd8701237c61f404253f707d` and docs/labs/ch22-identity.md.
307 portable cases and Android compilation pass. The optional Account page uses
Auth0 SDK 1.4.0 after public tenant/client configuration; source ships unconfigured.
Live tenant/device sign-in and screenshots remain pending. See verification.md.

## Chapter 23 — secure sessions and independent API authorization

Use source `50a5abf391e37fef04866c0528af4da195954354` and docs/labs/ch23-sessions-api.md.
321 portable and 37 API cases pass, as do Android compilation and the public listener
smoke. The API read is ownership-filtered and its default customer store is empty.
Provider and native-storage observations remain pending; see verification.md.

## Chapter 24 - provider-supported MFA and recovery lab

Configuration/documentation milestone only; application source remains
50a5abf391e37fef04866c0528af4da195954354. Follow docs/labs/ch24-mfa-recovery.md.
Real-provider and device observations remain pending; no MFA test total is claimed.

## Chapter 25 - account-isolated SQLite workspace

Executable source: 3309637a8abc06ecf1e65fafd1bec32afbbe315b.
345 portable cases, 37 API cases, Android compilation, both console labs and public
loopback smoke pass.
Follow docs/labs/ch25-account-lifecycle.md; simulated receipts establish local removal
behavior only. Native settings and real export/deletion remain pending.
