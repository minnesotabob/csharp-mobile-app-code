# Source milestones

## Part I foundation
Use commit 0b8973660cc21269ff79eb54733edaf428fb5dac to follow Chapters 1–5. It includes the first MainPage, NavigationPage startup, 37 passing portable tests, and recorded Android build evidence.

https://github.com/minnesotabob/csharp-mobile-app-code/tree/0b8973660cc21269ff79eb54733edaf428fb5dac

## Part II interface
The following commits develop Shell, portable view models, reusable views, localization, and width-based list/detail presentation. See docs/verification.md for the exact verified revision. Part I source remains available in history; current main intentionally has different startup/navigation.

No app-store release, runtime device validation, or chapter tag is implied by these source milestones.

Part II verified implementation: 9497b0d7e8acb74c07c50be8cb56f26453664fbb. Windows CI passed 61 portable tests and Android Debug compilation; native device and iOS verification remain pending. See verification.md for exact run links.

## Chapter 12 — catalog metadata and creator relationships

Use source `d496a51b86d1bb9b123745eecf58379695e62bbc` for the Chapter 12
catalog declaration, creator fixture and snapshot-validation walkthrough.

https://github.com/minnesotabob/csharp-mobile-app-code/tree/d496a51b86d1bb9b123745eecf58379695e62bbc

Later checkpoints extend CatalogItem and the import boundary. Retrieve this source
when reproducing the chapter's exact declarations; see verification.md for executed
build/test records rather than treating a source link as a new runtime result.

## Chapter 13

- Source: 51870ac298eff6198e098a063bdc391fecc665f8.
- Portable suite: 112 passing tests; Android Debug build passed.
- Adds combined discovery criteria, controlled input debounce and bounded device-local favorites.
- See verification.md for CI links; native execution and iOS remain pending.

## Chapters 14–16 — details, request form and simulated submission

The Draft V3 printed walkthroughs use source
`84889b8d6cfe7614cb748944fc2ec36ae149b42c` for the detail/browser boundary,
three-step request page, draft edit/save behavior and process-local pricing and
submission simulator.

https://github.com/minnesotabob/csharp-mobile-app-code/tree/84889b8d6cfe7614cb748944fc2ec36ae149b42c

This checkpoint deliberately contains the combined continuation for all three
chapters. Use fictitious inputs; its demo receipt is not an order or payment.
Historical execution records and subsequent corrections remain in verification.md.
The manuscript source audit checks excerpt fidelity; it does not add a new app run.

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

## Chapter 20 — local-first catalog and durable refresh intent

Use source `74d2df5ade5b1d69b306bdb1a406722d08057ed1` and
`docs/labs/ch20-offline.md` for the schema-3 extension, OfflineCatalogService,
controlled refresh ticket and real-file tests.

https://github.com/minnesotabob/csharp-mobile-app-code/tree/74d2df5ade5b1d69b306bdb1a406722d08057ed1

This is the portable coordinator/worker lab. It does not replace the native catalog
screen or register a native background scheduler. Consult verification.md for
execution history and the chapter's native worksheet for the integration journey.

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

## Chapter 26 - protected-read security boundaries

Source a3d2a880195714dc61a2971bc2ab3f7aab1d78a9: 360 portable and 46 API cases pass,
with Android compilation, both console labs and public loopback smoke. The threat
register maps actual checks to residual native/provider/deployment work. No complete
security certification, live penetration test or dependency-advisory result is implied.

## Chapter 27 - foreground audio and durable local checkpoints

Corrected source: c73f3cd25e175d7e89de84f3cbdaa9bacdd86af6. Portable 394 and API 46
cases pass, with Android compilation, both earlier console labs and public loopback
smoke. Follow verification.md for exact evidence and limits. The native foreground
page uses original bundled WAV fixtures; progress is anonymous and local. Playback,
seeks, foreground stop and iOS remain pending device observations. No background or
remote/customer-media capability is enabled by this milestone.

## Chapter 28

Source `a9e893d75689a48ecfbbe9f4e107fc5761780481`: separate opt-in background audio lab, portable playback owner and source fencing,
public original 60-second fixture, toolkit system session/metadata, Media3 focus/noisy
configuration and iOS pause-only interruption observers. The lab retains its mounted
host across an internal browse panel; Shell departure releases it. This is not an
app-wide or process-resilient service architecture. See background-audio-lab.md and
verification.md for exact source/results; every native worksheet row and iOS compile
remain pending.

## Chapter 29

Source `373dad1f2ebe14843989c3f4bb578e5a97b02b19` adds trusted public descriptors,
foreground native download/cancel/verify/delete, exact range resume, bounded reads,
length/hash promotion and storage budgets. The API embeds one original public WAV
with GET/HEAD/Range/If-Range support. 448 portable and 53 API cases pass; Android
compile and merged manifest pass. See verification.md and download-lab.md. Native
TLS/storage/eviction and iOS compilation remain pending; private deliveries and OS
background transfer scheduling are not implemented.

## Chapter 30

Corrected source `23e90f12d28060120d331d02a9e77d07a1091843` adds local notification
adapters, typed canonical links, real SQLite pending/receipt bounds, ready-window
routing, explicit private-read continuation and public share-sheet invocation. Optional
push registration uses current API leases and dedicated scope/owner checks, persisted
generations and a bounded volatile server registry. 488 portable cases and 62 API cases
pass; Android compilation and notification/link manifest assertions pass. See verification.md
for exact tested revisions. Native/FCM/APNs delivery and iOS compilation remain pending.

## Chapter 31

Attachment implementation begins at `64eac79b6fa795ecef58b15fa746caa29e641f58`;
use corrected workflow `82a68ba52c2a83b84d5c1f68be7d4e733312e338`. The local lab reads provider
streams, validates bounded text/image input, normalizes/inspects PNG output, fences
late scope work and exports verified copies to a narrow share root. Connectivity and
haptics are optional. 523 portable and 62 API cases pass at the implementation revision;
exact final native/configuration evidence is recorded separately. Customer uploads,
durable owner/draft composition, native codec/provider/share acceptance and iOS compile
remain pending. No camera capture or location permission is added.

## Chapter 32 / Part VII

Final executable source `6e5921d05cc25fa965a2c93a326e571228d1f0d9` adds opt-in
server-owned canonical quotes, immutable teaching price snapshots, SQLite atomic
order/history acceptance, owned operation replay before expiry, dedicated read/write
scopes and a durable account/resource-partitioned client journal. 552 portable and
77 API cases pass; Android compiles cleanly. API/core source is unchanged from its
verified `95ca22e178978f6bf9ce7558c018d5e5339a8319` run; later UI refinements expose
all quote fields and clear displayed receipts on route departure. Exact runs are in
verification.md. See commerce-lab.md for configuration and acceptance worksheet.
Accepted is not paid or real production. Live identity/device/iOS, privacy lifecycle,
verified payment, full order tracking/private delivery and operational recovery remain
pending separate boundaries. The lab does not upload earlier local attachments.

## Chapter 33

Final executable source `bbeb33e201ffa0cc233bfd1e7991aad4fe9ca416` adds Development-only
Stripe TEST checkout reservations/adapter, fresh exact-byte signature verification,
bound session/amount/owner evidence and atomic replay-safe payment state. 590 portable
and 87 API cases pass; Android compilation passes. Browser returns cannot mark paid;
mobile can refresh owned TEST status and has no checkout launcher. Canceled orders
cannot create/reuse checkout or expose the URL; previously obtained provider URLs may
remain usable and late paid evidence requires review. See verification.md/payment-lab.md.
Actual hosted provider/CLI, native/iOS and storefront eligibility remain pending. No
real-money lifecycle, refunds/disputes, full ledger or production authorization is supplied.

## Chapter 34

Final executable/fixture source `4a4ba828dc7d32dee68c8334da9f8687c2c000ff` adds owned
paging/coherent tracking snapshots, durable revisions, atomic state/history/notices,
controlled review/delivery and short-lived hashed owner/version grants. 617 portable
and 99 API cases pass; Android compiles cleanly. API/core policy is unchanged from its
verified `025d86e13ac447b5cdcf2f4ddadf17e0d2dfa7e6` revision; later changes add the
runnable synthetic fixture, clean test analyzers and correct native event nullability.
The actual HTTP/client journey retrieves exact original WAV bytes. Native page verifies
memory only; private listening/cache, real administration/customer artifacts, provider
notices, privacy lifecycle and iOS remain pending. See verification.md/fulfillment-lab.md.

## Chapter 35 / Part VII completion

Final executable source `89eeecf23fffa599fbc3f262f79f711f8624afe4` adds explicit commerce
schema 1, verified online backup/new-directory restore, grant invalidation, generation
review and generic readiness. The operations profile supplies allowlisted logs/metrics/
traces, a bounded controlled failure, separate local diagnostics consent and a separately
consented newsletter port. 636 portable and 107 API cases pass; Android compiles cleanly.
The published HTTPS staging workflow actually verifies restored hold/release, preserved
payment replay, invalidated old grants and correlated redacted failure output. It is a
loopback synthetic drill, not public provisioning or real provider/customer recovery.
Native UI, managed/native fatal-crash SDK, remote exporters, real newsletter/provider
integration and iOS remain pending. See operations-lab.md and verification.md.

## Chapter 36 / Part VIII begins

Executable acceptance source `dd137ed10809a40ae38217b485b331fe33552760` adds two composed
client/API recovery rows and a fresh same-job requirement report. Actual journals and
server SQLite reopen across lost committed responses, preserved operation identity,
TEST payment replay, revision after newer review, private WAV verification and foreign
account denial. C# totals are 636 portable and 109 API; the separate Python evidence
projector has 18 checks. Exact successful runs are in verification.md. The selected
six-requirement spine is not full requirement coverage; six manual rows remain not_run
and release readiness stays unassessed. TestServer transport, synthetic identity and
checkout fixtures remain distinct from the unchanged Chapter 35 published HTTPS/native
compile evidence and unexecuted live provider/native/iOS acceptance.

## Chapter 37

Executable source `c82eff270ec50c3ef4916c4401576ba1b893b1a9` adds stable sorted search
snapshots, a bounded deterministic large catalog, original raw image variants, a native
stress page and a Release investigation runner. 644 portable/109 API cases, 18 evidence
projector checks, Android compilation, published HTTPS recovery and the performance
workflow pass. Measured five-query median changes from 107.7073 to 93.4096 ms on a
shared Windows VM; allocations fall from 633,032 to 140,896 bytes, with construction
recorded separately. These are desktop observations, not native acceptance or a speed
guarantee. Source/run/raw sample JSON and the incomplete device/accessibility review
are retained in docs/reviews. Native startup/frames/decoded memory/energy/screen readers,
full trim/AOT compatibility, signed packages/install/upgrade and iOS remain pending.

## Chapters 39–40 release preparation

Chapter 39 adds iOS toolchain/signing/TestFlight procedures and a pending worksheet;
no iOS build/signing/device/store execution. Chapter 40 implementation
22489d1906110ebf4925aa6b2a2b04c0b2833130 provides an executed synthetic packet lab
and 17 passing local Python boundary tests. See release-maintenance.md. These
milestones do not complete production/native/provider acceptance or create edition tags.
