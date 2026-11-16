# Chapter 34 tracking, revisions and private delivery — November 16, 2026 (Central)

Final executable/fixture source: `4a4ba828dc7d32dee68c8334da9f8687c2c000ff`.
SDK 11.0.100; C# 15; MAUI 11.0.10; Android minimum API 26.

- Portable Release build: **0 warnings/errors; 617 cases pass** (27 new fulfillment
  cases), no errors/failures/skipped/not-run cases. Chapter 21/25 console labs and the
  new Chapter 34 synthetic fulfillment console smoke execute successfully.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43825891878
- API/probe Release builds at unchanged API/core implementation
  `025d86e13ac447b5cdcf2f4ddadf17e0d2dfa7e6`: **0 warnings/errors; 99 cases pass**
  (12 new fulfillment cases), no errors/failures/skipped/not-run cases. Public Kestrel
  catalog/media range/hash loopback smoke passes.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43823960239
- Android Debug compilation: **success; 0 final compiler warnings/errors**. Existing
  merged media/notification/link/capability/share configuration assertions pass.
  Dependency setup retains its historical unresolved cmdline-tools;21.0 warning,
  separate from final compilation.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43825891872

Initial source passed all 617/99 cases and Android compilation. Two xUnit analyzer
warnings (Single predicate and test cancellation) and a nullable Home event-handler
warning were corrected. A runnable synthetic local fixture and CI smoke were added;
application policy/core/API code is unchanged from its verified initial implementation.
The final Android run checks the corrected native handler. No compiler pass is treated
as native identity, file-read, accessibility or listening evidence.

Real temporary SQLite tests cover owned live paging, coherent tracking/history/notices,
revision replay before later version/state checks, changed-key facts, stale/foreign/
invalid/capacity outcomes, two independent writer connections and forced notice-insert
rollback. They cover atomic/replayed delivery publication, hashed owner/version-bound
grants, exact expiry/renewal, capacity, reopen, revocation and publication replay that
cannot restore revoked access. New controlled publication requires verified TEST paid
state; legacy graph-only fixtures cannot substitute for it.

The production revision journal/coordinator is tested for reopen/account/resource
partitioning, unresolved replacement blocking, local persistence failure before HTTP,
committed-but-lost reply with both databases reopened after a later review, and sign-out
before a delayed committed reply. Simulated HTTP loss is explicit; database operations
are real. Private-client tests verify the exact original WAV, corrupt/oversized/wrong
media rejection and owned-buffer clearing on disposal.

Signed JWT in-process API integration exercises actual quote/accept HTTP, controlled
signed synthetic payment/production/review, customer revision, later review/delivery,
production TrackingApiClient/coordinator and exact authorized WAV bytes. It verifies
no-store, nosniff, strict revision fields, owned list/detail scopes, foreign/anonymous/
missing/expired/revoked/wrong-delivery access, query-grant rejection, unsupported Range
and absent static fixture path. This is an actual service/client contract test with
controlled publication/payment, not a real commission, provider charge or native read.

Native page supports explicit owned pages/history/notices, save/send/retry revision and
memory-only verified sample retrieval. It creates no private cache, player or share file.
Notices are durable in-app observations, not APNs/FCM/email receipts. Grants last two
minutes and remain bound to JWT owner/current artifact/version/state; expiry/revocation
cannot erase a response already authorized. VerifiedDelivery clears its owned array,
not all managed/OS/transport copies. The WAV is original generated public-source test
content; route tests establish access policy, not secrecy of those known sample bytes.

FulfillmentLab smoke uses a temporary database. Its explicit local seed/review/deliver/
revoke commands require GNAZZO_SYNTHETIC_FIXTURES=1 and direct disposable-development
database authority. They manufacture signed TEST payment evidence and are never real
administration or production authorization. See fulfillment-lab.md for runnable commands,
configuration, protocol, artifact provenance and pending acceptance matrix.

**Pending:** native identity/TLS/list/selection/editor/lifecycle/screen-reader/large-text
and memory verification observations; iOS compilation; actual private listening/offline/
export composition; real customer/admin/artifact ingestion, provider push/email, complete
production authorization, replacement/retention/migration/audit/backups/recovery and
privacy export/deletion composition. Sign-out retains original-owner pending revisions;
logical revocation and buffer clearing are not physical erasure.

---

# Chapter 33 payment boundaries and verified events — November 16, 2026 (Central)

Final executable source: `bbeb33e201ffa0cc233bfd1e7991aad4fe9ca416`.
SDK 11.0.100; C# 15; MAUI 11.0.10; Android minimum API 26.

- Portable Release build: **0 warnings/errors; 590 cases pass** (38 new payment
  cases), no errors/failures/skipped/not-run cases. Earlier Chapter 21 and 25 console
  labs also execute successfully.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43819553353
- API/probe Release builds: **0 warnings/errors; 87 cases pass** (10 new payment
  cases), no errors/failures/skipped/not-run cases. Public Kestrel catalog/media
  range/hash loopback smoke passes.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43819553341
- Android Debug compilation: **success; 0 final compiler warnings/errors**. Existing
  merged media/notification/link/capability/share configuration assertions pass.
  Dependency setup retains the historical unresolved cmdline-tools;21.0 warning,
  separate from final compilation.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43819553425

The initial 583-case run compiled cleanly but failed one independently generated signed
fixture: Windows Git checkout converted its JSON line endings. The verifier correctly
rejected altered raw bytes. A -text attribute preserves that protocol fixture; the
corrected 588-case suite passed. Final review then added canceled-order checkout/URL suppression and canonical HTTPS
return-origin regression cases, yielding 590. All final application/core/API source is at
the revision above. The signature implementation was not weakened to accept normalized
messages.

The restricted direct-account Stripe TEST adapter verifies account, fixed API origin,
explicit API version, stable server idempotency key, immutable USD amount and hosted
checkout destination. Live keys are rejected. Whole-operation deadline and response
bounds apply. Payment routes require Development + separate commerce/payment opt-ins;
no provider credentials are shipped. The native order page only refreshes owned TEST
status and never launches checkout. Browser return/cancel pages never write state.

Real temporary SQLite tests cover durable reservations/reopen, provider reply loss and
stable key reuse, 23h automatic retry cutoff, verified paid/unpaid/failure/expiry policy,
two-connection duplicates, forced payment-update rollback, event-ID fact conflicts,
amount/session/account binding, payment-reference conflicts, canceled-order review,
controlled paid production gate and suppression of canceled checkout creation/URLs.
Crypto tests cover tampering, stale/future delivery, wrong secret, duplicate timestamp,
multiple v1 signatures, live/Connect/duplicate-JSON rejection and the independent Python
vector. Provider HTTP contract tests use a simulated handler, not api.stripe.com.

Signed in-process JWT/API integration reserves/attaches a simulated hosted session,
applies signed synthetic webhook bytes, reads one owned Paid receipt and proves that
forged return/cancel queries cannot mark Paid. It checks scopes, empty checkout bodies,
no-store, foreign-owner URL privacy, missing/oversized signatures, ordering and retryable
unbound events. The service never changes production order state on payment receipt.
StartProductionForPaymentLab is a controlled fixture only; Chapter 32 AdvanceForLab
remains a legacy graph fixture and is not a real production/payment authorization path.

**Pending:** actual hosted Stripe sandbox/CLI/test-card/API-version execution; native
identity/TLS/status/switching/storage/accessibility observations and iOS compilation;
storefront/product/region/program eligibility and approval. Refunds/disputes, ledger,
payment replacement, automatic provider reconciliation sweep, fulfillment outbox,
financial audit, migration/retention/backup/recovery and real administrative production
authorization are not implemented. Unsupported signed events are acknowledged without
effect, not financially reconciled. Existing provider URLs are not revoked by local
order cancellation; later success requires ReviewRequired. No real payment or public
checkout is supplied. See payment-lab.md for setup, protocol and acceptance matrix.

---

# Chapter 32 owned quotes/orders — November 15, 2026 (Central)

Final executable source: `6e5921d05cc25fa965a2c93a326e571228d1f0d9`.
SDK 11.0.100; C# 15; MAUI 11.0.10; Android minimum API 26.

- Portable Release build: **0 warnings/errors; 552 cases pass** (29 new commerce
  cases), no errors/failures/skipped/not-run cases. Both earlier synchronization and
  account lifecycle console labs execute successfully.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43814370719
- API/probe Release builds at unchanged API/core source
  `95ca22e178978f6bf9ce7558c018d5e5339a8319`: **0 warnings/errors; 77 cases pass**
  (15 new commerce cases), no errors/failures/skipped/not-run cases. Kestrel public
  catalog/media range/hash loopback smoke passes.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43813143415
- Android Debug compilation: **success; 0 final compiler warnings/errors**. Earlier
  merged capability/share/media/notification/link configuration assertions pass.
  Dependency setup retains its historical unresolved cmdline-tools;21.0 warning,
  separate from final compilation.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43814370792

The initial implementation passed 552 portable/77 API cases and Android compilation
at `95ca22e178978f6bf9ce7558c018d5e5339a8319`. Subsequent native-page review exposed
all vocal/explicit fields for review and clears the displayed order receipt on actual
route departure; final portable/Android runs above verify the revised composition.
The API, portable policy and tests are unchanged by those UI/documentation refinements.

Real temporary SQLite tests exercise server-calculated/canonical/immutable price
snapshots, two independent writer connections, same-key replay, different-key quote
reuse, cross-owner/issuer lookup, expiry, changed payload, forced history-insert rollback,
optimistic state versions and terminal transitions. Journal tests prove partitioning,
reopen, unresolved replacement blocking, rejection policy, receipt version checks and
redacted record text. Production coordinator tests prove persistence failure sends no
HTTP and committed-but-lost reply survives both databases reopening after quote expiry.
Controlled delayed HTTP verifies A's sign-out prevents late publication under B.

Signed JWT API integration covers default-disabled routes, dedicated scopes, owner
predicates, no-store, strict bounded JSON and absence of customer state/payment updates.
The production OrderApiClient/coordinator also create/accept/persist and GET-refresh a
controlled fixture version through the actual middleware. These are in-process tests,
not live provider or device/TLS evidence. Server AdvanceForLab is trusted in-process
simulation only; it has no customer HTTP route and supplies no payment authorization.

The opt-in API stores synthetic plaintext quotes/orders/history in a separate v1 SQLite
file. Quoted historical amounts are teaching snapshots, not live offers. Accepted means
recorded request, not paid or real production. App journal stores one last operation per
issuer/subject/origin/audience (16 partitions), no tokens, and persists before POST and
before UI success. Unresolved ambiguity retains the original key. Only ExpiredQuote,
ChangedRequest and Unavailable are definitive post-replay rejection codes. Sign-out
retains original-owner journal for reconciliation; it is not a physical purge.

**Pending:** live tenant grants/sign-in/refresh/sign-out; native TLS, callback/browser,
route/window, restart/storage/accessibility observations; iOS compilation; payment and
real production authorization (Chapter 33), full owned history/private delivery (34),
privacy export/deletion composition, schema migration/retention/backups/distributed
budgets and operational audit (35). Historical quote rows count against lab capacity;
there is no automatic cleanup. Customer attachment upload remains separate.
See commerce-lab.md for configuration, protocol, state graph and acceptance worksheet.

---

# Chapter 31 native capabilities and attachments — November 15, 2026 (Central)

Corrected workflow/executable source: `82a68ba52c2a83b84d5c1f68be7d4e733312e338`.
SDK 11.0.100; C# 15; MAUI 11.0.10; Android minimum API 26.

- Portable Release build: **0 warnings/errors; 523 cases pass** (35 new attachment/
  fallback cases), no errors/failures/skipped/not-run cases. Earlier Chapter 21 and
  25 console labs execute successfully.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43809402430
- API/probe Release builds at unchanged API/core parent
  `64eac79b6fa795ecef58b15fa746caa29e641f58`: **0 warnings/errors; 62 cases pass**,
  no errors/failures/skipped/not-run cases. Kestrel public catalog/media range/hash
  loopback smoke passes. No attachment endpoint or upload is added.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43808574847
- Android Debug compilation: **success; 0 final compiler warnings/errors**. Merged
  network-state/haptic permissions, absence of camera/location permissions, one narrow
  source cache root and merged MAUI FileProvider resource reference pass, along with
  earlier media/notification/link checks. Dependency setup retains its historical
  unresolved cmdline-tools;21.0 warning, separate from final compilation.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43809402360

Initial source compiled without warnings and passed portable/API checks. Its sharing
assertion used PowerShell XML .Name, which was shadowed by the cache-path name attribute.
The corrected XPath verifies exactly one cache-path with the intended name/path and
emits source/merged provider XML. Executable app/core/API code is unchanged by that
workflow correction. This is configuration evidence, not an actual content URI/grant.

New tests use real temporary files and independently generated exact synthetic PNGs.
They cover strict text/byte limits; provider stream disposal/nonseekable reads; prior
selection preservation/replacement; image header/pixel/output bounds; CRC/framing and
ancillary removal; late/canceled/stale scope work; immediate invalidation versus cleanup
confirmation; stored integrity; aggregate quota; verified generic exports, metadata/hash
rejection, count/expiry/cleanup policy; and disabled/unsupported haptic fallback.
The normalizer is a simulated port: no portable test decodes/resizes a native bitmap.

Native lab code uses OpenReadAsync, one-result media selection, requested processing,
PlatformImage normalization and explicit exported-copy sharing. One transient local
synthetic-reference scope is separate from customer drafts/accounts. Metadata stripping
removes accepted PNG ancillary chunks, not private visual/text content or all OS/provider
copies. Cache is unencrypted and replaceable. The staging budget includes abandoned
scope files but is not a complete cache inventory or cross-process lock.

**Pending:** iOS compilation; actual provider permissions/URI/cloud access; native
codec/memory/format/orientation/metadata/alpha/color results; late picker/cancellation,
process/cache/write behavior; Android share URI/grants, phone/iPad presentation and
asynchronous receiver reads; connectivity events and haptic/system settings. The picker
can preprocess before our byte guard and tokens cannot preempt synchronous codec work.
Customer upload, durable authenticated owner/draft attachment composition, orphan
inventory and optional location implementation are not supplied. No camera capture or
location permission is enabled; local cleanup cannot revoke receiver copies or prove
physical erasure. See capability-attachment-lab.md for every pending native row.
Historical records follow.

---

# Chapter 30 notifications, links and sharing — November 15, 2026 (Central)

Corrected executable source: `23e90f12d28060120d331d02a9e77d07a1091843` on main.
SDK 11.0.100, C# 15, MAUI 11.0.10; Android minimum API 26.

- Portable Release build: **0 warnings/errors; 488 cases pass** (40 new cases),
  no errors/failures/skipped/not-run cases. Earlier Chapter 21 and 25 console labs
  execute successfully.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43772049238
- API/probe Release builds at unchanged API/core parent
  `09ed64e9d2cb16851b080d0a9816b767b1984f24`: **0 warnings/errors; 62 cases pass**
  (9 new cases), no errors/failures/skipped/not-run cases. Actual ApiSession HTTP sink
  registers/removes through signed-token middleware; owner/scope/generation/body/cap
  checks pass. Kestrel public catalog/media range/hash loopback smoke still passes.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43771527612
- Android Debug compilation: **success; 0 final compiler warnings/errors**. Merged
  POST_NOTIFICATIONS permission and VIEW/DEFAULT/BROWSABLE custom scheme/host filter
  assertions pass, along with earlier media service assertions. Dependency installation
  retains the historical cmdline-tools;21.0 warning, separate from final compilation.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43772049172

Initial native compilation exposed nullable NotificationManagerCompat.From results;
the corrected adapter handles absence explicitly. The first manifest assertion wrongly
required host and scheme on one data element. Generated XML uses separate data elements
inside one intent filter. The corrected assertion verifies their collective filter
semantics and emits the exact filter; it does not claim a native activation result.

Portable cases use real temporary SQLite for persistence/reopen, pending coalescing,
receipt replay, expiry, clear and capacity. Parser/payload cases reject unapproved
routes, oversized/unknown/duplicate JSON fields and invalid lifetimes. Simulated push
sink cases cover permission loss, rotation, persist-before-send, restart generation,
failed persistence/removal and redaction. API cases verify authenticated dedicated
scope, owner isolation, generations, no token echo/no-store, limits, expiry, token
uniqueness and actual HTTP sink registration/removal. No test prompts/displays/taps,
shares to a person or delivers a real push message.

Native local adapters, canonical custom-scheme links, bounded routing inbox, explicit
private-read continuation and public share-sheet code are present. The optional push
registration path is implemented/tested but no FCM/APNs acquisition SDK, provider
project/key, delivery sender/receiver or durable registry is configured. Custom links
are not verified HTTPS association and have no browser fallback. Server registration
shape/permission does not prove provider token validity or device possession.

**Pending:** iOS compilation; actual permission/channel presentation, OS warm/cold tap
ordering, queued readiness, private read cancellation on device, share-sheet behavior,
provider token rotation/account cleanup/outbox and live FCM/APNs delivery. SQLite hint
metadata is unencrypted; backup/shared-device privacy requires review. No exactly-once
navigation guarantee is claimed. See notification-link-lab.md for all native rows and
remaining account/provider composition gates. Historical records follow.

---

# Chapter 29 public downloads — November 15, 2026 (Central)

Executed source: `373dad1f2ebe14843989c3f4bb578e5a97b02b19` on main.
SDK 11.0.100; C# 15; MAUI 11.0.10; Android minimum API 26.

- Portable Release build: **0 warnings/errors; 448 cases pass** (35 new download
  cases), no errors/failures/skipped/not-run cases. Earlier Chapter 21 synchronization
  and Chapter 25 account lifecycle console labs execute successfully.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43769261598
- API/probe Release builds: **0 warnings/errors; 53 cases pass** (7 new media/API
  cases), no errors/failures/skipped/not-run cases. Actual ASP.NET range handling and
  actual downloader cancellation/resume through that server pipeline pass.
  Real Kestrel loopback HTTP smoke passes health/catalog/paging, full media hash and
  an exact matching Range/If-Range byte slice. This is not native/TLS evidence.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43769261609
- Android Debug compilation: **success; 0 final compiler warnings/errors**; existing
  merged background-audio service permission/type assertions pass. Dependency setup
  retains the historical unresolved cmdline-tools;21.0 warning, separate from compilation.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43769261603

Portable cases use real temporary files with simulated HTTP/free-space conditions.
They cover full verification/cache hit, cancel and restart, transport interruption,
validated 206 resume, full 200 truncation, 416 discard, wrong range/ETag/encoding/type/
length, weak validator, hash mismatch, short/oversized bodies, complete-part recovery,
corrupt final rejection/replacement, quota/reserve/low-space failure, serialized deletion,
safe identities/origins, private-descriptor rejection and redirect/error handling.
API cases independently check original body/hash/strong ETag, exact slices, changed
If-Range, 416, HEAD, absent private route and actual client cancel/resume.

Native Home opens a separate foreground public-download lab. CacheDirectory holds a
single-writer public folder with a 4 MiB quota and 64 KiB free-space reserve. Trusted
fixed-path descriptors supply original WAV length/hash; full verification precedes
promotion and availability. Cancel/page departure/window stop never schedules an
automatic restart. No .part path is returned as available media.

**Pending:** iOS compilation; physical-device HTTPS trust/reachability, cancellation,
process restart, cache eviction, competing storage consumption and actual write/rename
failures. A space check is not a reservation; rename is not power-loss durability.
Private delivery descriptors are rejected rather than routed into anonymous storage.
Authenticated delivery service/access, account-scoped publication, encryption/backup,
OS background transfers and downloaded-file playback integration are not implemented.
See download-lab.md for every native row and current limits. Historical records follow.

---

# Chapter 28 background audio — November 15, 2026 (Central)

Corrected executable source: `a9e893d75689a48ecfbbe9f4e107fc5761780481` on main.
SDK 11.0.100, C# 15, MAUI 11.0.10, MediaElement 12.0.0; Android minimum API 26.

- Portable Release build: **0 warnings/errors; 413 cases pass**, no errors/failures/
  skipped/not-run cases. The 19 added cases are 18 simulated playback-owner cases
  plus the third original WAV header/duration/hash case. Both earlier console labs
  (Chapters 21 and 25) execute successfully.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43767142547
- API/probe (unchanged service/probe/test source at parent
  `bedeec9000f65a3492989fe80778bd7f1a3e76b6`) Release builds: **0 warnings/errors; 46 cases pass**, no errors/failures/
  skipped/not-run cases; real Kestrel public loopback smoke passes.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43766620130
- Android Debug compilation: **success; 0 final compiler warnings/errors**.
  Merged manifest assertions pass for both foreground-service permissions and the
  toolkit mediaPlayback service type. Dependency installation retains its historical
  unresolved cmdline-tools;21.0 setup warning; this is separate from final compilation.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43767142544

The initial check exposed two actionable defects: a remote Pause observation did not
capture its accompanying position, and pinned Toolkit 12.0.0 does not expose
MediaManager.Player to this application. The corrected owner applies the pause's
position before saving; the handler uses the public native PlayerView hierarchy and
IExoPlayer interface, failing visibly if absent. This is a toolkit-upgrade compatibility
dependency. The corrected tests pass cancellation tokens and compile without warnings.

New portable cases exercise no-autoplay loading; default stop; opted-in continuation;
foreground-only loading; opt-in revocation; canceled/late preparation; stale source
observations; remote pause checkpointing; no application resume Play; terminal release;
failed save/retry; cleanup uncertainty; unavailable media; disposal; actual seek position;
and restoration without autoplay. They use a simulated native port, not device audio.

The app keeps one mounted lab host while toggling an internal browse panel. Shell
departure/window destruction releases it. Global toolkit service capability is enabled,
but Chapter 27 elements explicitly disable it. The background handler delegates Android
focus/noisy behavior to Media3; iOS adds audio mode and pause-only observers. Metadata
is public fixture information. The finite original 60-second WAV is not a keep-alive.

**Pending:** iOS compilation, native launch/decoding, actual system controls, focus gain/
loss, calls, wired/Bluetooth routes, task removal, force-stop, process termination and
power constraints. The foreground timer is best effort, not guaranteed background
execution. No app-wide playback host, production MediaSessionService architecture,
process restoration, streaming URLs, customer media or authenticated delivery is claimed.
See background-audio-lab.md for every pending device row. Historical records follow.

---

# Chapter 27 foreground audio - November 15, 2026 (Central)

Corrected source: `c73f3cd25e175d7e89de84f3cbdaa9bacdd86af6` on main.
SDK 11.0.100; C# 15; MAUI 11.0.10; CommunityToolkit.Maui.MediaElement 12.0.0.
The compiled bootstrap uses CommunityToolkit.Maui and the actual parameter name
isAndroidForegroundServiceEnabled: false. Initial native checks exposed the overview's
namespace and parameter mismatch; the matching source below contains both corrections.

- Portable Release build: **0 warnings/errors; 394 cases pass**, including 34 new
  playback/storage/fixture cases; no errors/failures/skipped/not-run cases. Existing
  Chapter 21 and Chapter 25 console labs also execute successfully.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43764322521
- API/probe Release builds: **0 warnings/errors; 46 API cases pass**, no errors,
  failures/skips/not run; public loopback smoke passes.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43764322453
- Android Debug compilation: **success; 0 final compiler warnings/errors**.
  Dependency setup retains the documented unresolved cmdline-tools;21.0 warning.
  https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43764322496

Android minimum changes from API 24 to 26; the activity uses resize/single-task setup.
No background service/audio entitlement is enabled by this lab. Original bundled PCM
WAV files (10/14 seconds, mono 16-bit 8000 Hz) have generation source and SHA-256 manifest.

The 34 new portable cases use simulated native observations and real temporary SQLite.
They cover no-autoplay readiness, source ticket fencing including A-B-A, bounded seek
and observed position, completion/replay, failure, item/revision restart, rewind,
corruption and injected write failure/retry. Fixture tests verify source headers,
duration and hashes. They do not establish actual decoding or audible output.

Limits: anonymous unencrypted local progress, not account synchronization. Small
synchronous SQLite writes, one page/foreground owner and no arbitrary termination save
guarantee. No public/private streaming transport, downloads, native background controls
or account migration is added. Device playback, seek rounding/cancellation, lifecycle,
phone/focus/headphones, actual disposal, iOS build/runtime and screenshots remain pending.
Follow docs/labs/ch27-audio-progress.md and T12-T14 in docs/security/threat-model.md.

## Earlier verification records

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
