# Gnazzo's World - Chapter 26 threat model

Scope: source a3d2a880195714dc61a2971bc2ab3f7aab1d78a9 and earlier labs. This is a
focused design/rejection-test record, not a penetration-test report, MASVS certification
or release approval. Only owned fixture tests are run. Live provider/native/deployment
observations remain separate.

## Assets, actors and trust boundaries

Assets: access/refresh credentials, customer song stories/previews, account-scoped
preferences and lifecycle intents, distributable media, audit records and build secrets.
Actors: anonymous visitor, signed-in customer A, another customer B, malicious link/source,
lost/shared device, operator and dependency/build supplier. A signed-in customer can be
an attacker against another owner's records.

Boundaries: OS/browser callback -> SDK -> accepted session; untrusted route/content ->
validated stable ID; native HTTP -> configured HTTPS API; bearer middleware -> permission
-> owner-filtered lookup; account activation -> local SQLite partition; CI/package source
-> built artifact; application decision -> restricted diagnostic/audit sink.

The API currently exposes public catalog reads and one protected preview read. It has no
customer write/file-download/lifecycle route, production customer fixtures or private token
signing key. Sync, lifecycle receipts and some provider boundaries are explicit labs.

## Threat register

| ID | Abuse case and affected asset | Enforced control/evidence | Residual work / owner |
|---|---|---|---|
| T01 | B guesses A's request ID | JWT scope + issuer/subject owner lookup; RequestAuthorizationTests | Real service data provisioning and authorization review / API |
| T02 | Supplied URL/path becomes private destination | RequestIdPolicy, fixed HTTPS API origin, disabled redirects in native handler; SecurityBoundaryTests and ApiSessionTests | Real TLS/proxy/redirect observation / native + hosting |
| T03 | Link launches lookalike provider/publisher | Reviewed host/callback bounds; SecurityBoundaryTests, SignInTests, OidcEngineTests | App-link registration/custom-scheme interception on real OS / native |
| T04 | Old account result appears after switching | ApiSession generation + AccountWorkspace instance handle; existing session/workspace tests | Native UI dispatcher and cache clearing composition / native |
| T05 | Repeated authorized reads consume lookup/log resources | Shared eight-per-minute fixed-window budget, queue 0; ProtectedReadSecurityTests | Auth/edge/public-route protection, load/fairness and distributed limits / hosting |
| T06 | Proxy or client stores private preview | no-store on protected prefix, including rejected responses; integration test | Real intermediaries, app caches/screenshots/backups / native + hosting |
| T07 | Request content/token leaks through audit | Event 2601 has four allowlisted outcomes only; capture-provider test | Framework/proxy/provider logs, audit access/retention/tamper protection / operations |
| T08 | Removed profile recreated by delayed work | Durable intent, local write freeze, monotonic receipt + transactional purge; AccountWorkspaceTests | Server/other-device freeze, provider removal and backup policy / API + operations |
| T09 | Lost device exposes plaintext data | Session vault adapter exists; local workspace is explicitly unencrypted | Native keychain/keystore, file protection/backup and retention evidence / native |
| T10 | Vulnerable package or leaked CI/backend credential enters release | Pinned versions; least-privilege read-only workflow permissions; documented package review commands | Current advisory scan, secret scan, dependency/update response and production secret store / build + operations |
| T11 | Request ID becomes customer-file path | No customer-file endpoint; private IDs are bounded data | Design authorized opaque lookup, safe storage mapping, delivery expiry/revocation before adding media/customer delivery / API |

Prioritize unauthorized private reads, credential disclosure and cross-account publication
before cosmetic exposure. Record reachability and evidence, not just a severity label.
Rate limiting does not repair authorization, and low identifier entropy does not justify
trusting the client. Native rooted-device resistance and application-integrity controls
are not supplied by this draft.

## Enforced Chapter 26 HTTP contract

Program.cs order is explicit routing, no-store prefix handling, authentication,
authorization, then rate limiting and endpoint execution. Unauthorized/underscoped
calls return before consuming the protected budget. They still cost authentication
resources; this policy is not edge or DDoS protection. The named budget is shared across
subjects within one process, resets with the process, allows fixed-window boundary
bursts and does not give fair per-user service. It is a deliberately small teaching
value, not production sizing. Public routes remain outside it.

Authorized protected calls with an available permit next validate the ID. Invalid shape
returns 400; unknown/other-owner records return the same generic 404; an owner gets 200.
Budget exhaustion returns 429, Retry-After and a generic problem response. Missing scope
still yields 403 before the limiter. Sensitive bodies are never copied into event 2601.

The audit projection records invalid_identifier, granted, unavailable or rate_limited.
It intentionally omits account identity, operation/request IDs, path/query, headers,
body and raw exceptions. It does not report every 401/403, correlate a deletion or provide
an immutable security audit trail. Production audit schema, access and retention are
separate design work. Default framework/proxy logging is outside this projection test.

## Dependency and secret review worksheet (not executed by the chapter tests)

Use the pinned SDK. Restore the selected project first. From the repository root:

```powershell
dotnet package list --project tests/GnazzosWorld.Core.Tests --include-transitive --vulnerable --format json --output-version 1 --no-restore
dotnet package list --project tests/GnazzosWorld.Api.Tests --include-transitive --vulnerable --format json --output-version 1 --no-restore
```

On the documented native workstation after Android restore/build, review the App project
with --framework net11.0-android too. Repeat on the iOS-capable environment for its resolved
graph. The portable solution alone does not cover every native SDK dependency. Record
source/date/SDK/resolved graph/advisory feed, exit status, findings and disposition. An
unavailable or failed feed is unknown, not zero findings. Review advisories and reachability;
upgrade deliberately and repeat matching tests/builds. No package-advisory scan result is
claimed from compilation or this worksheet.

Tenant/client/audience identifiers are public configuration, not permission. No native
client secret, Management API credential, production signing key or customer fixture belongs
in this repository. Test JWT keys are generated per factory. Development user-secrets keeps
values outside source control but is not encrypted production secret storage. Production
needs a controlled backend secret mechanism, least privilege, rotation and log protection.

## Pending observation record

For each T-ID record source/device/OS or service environment, exact scenario, observed
result, restricted evidence reference, owner and next action. Pending: native callback
interception/browser return; real TLS rejection/redirect; secure storage/reinstall/backup;
account switch/UI publication; provider MFA/recovery; full lifecycle/file authorization;
package/secret scans; proxy log redaction; load/distributed limits and production secrets.

Sources: OWASP MASVS control families (https://mas.owasp.org/MASVS/03-Using_the_MASVS/),
Microsoft rate limiting (https://learn.microsoft.com/aspnet/core/performance/rate-limit),
.NET package list (https://learn.microsoft.com/dotnet/core/tools/dotnet-package-list),
development secrets (https://learn.microsoft.com/aspnet/core/security/app-secrets).
The retrieved rate-limit/secrets guidance is the .NET 10 documentation view; actual
.NET 11 source behavior is established separately by matching CI evidence.

## Chapter 27 media extension (November 15, 2026)

T12: source substitution and late events. Current playback accepts only bundled WAV
resource names, creates a fresh native element/ticket per selection and checks ownership
again after dispatch. PlaybackTests reject URL/path substitution, old-source events and
A-B-A tickets. Native callback/disposal and actual decoder behavior remain pending.

T13: progress collision and privacy. Checkpoints include media ID/revision; this database
is explicitly anonymous to the installation, not account-owned or encrypted. It must not
be automatically assigned to a signed-in account. Tests cover revision isolation and
real write failure; future account migration/consent/deletion needs a separate contract.

T14: unintended continued sound. The page requests save/stop and invalidates work on
navigation/Window.Stopped, with foreground service disabled. Compile/policy evidence
cannot establish audible stop, audio focus, call/headphone behavior or lock-screen state.
Those device observations and Chapter 28 system integration remain pending.

## Chapter 28 — system-visible media and ownership

| ID | Risk | Current control | Remaining evidence |
| --- | --- | --- | --- |
| T15 | Private listening metadata exposed on lock screen | Only public original fixture titles/neutral artist are supplied | Device inspection and private-delivery metadata policy pending |
| T16 | Unexpected background start or audio after interruption | Foreground-only load, opt-in continuation, canceled loading, no app resume Play, native focus/noisy handling | Android/iPhone route, focus, call, task-removal and power worksheet pending |
| T17 | Duplicate or stale system player/session | One mounted lab host, toolkit command ownership, source tickets, terminal release | Native release/notification disappearance and toolkit upgrade checks pending |

The handler depends on a public native PlayerView hierarchy in pinned Toolkit 12.0.0, fails visibly when absent and uses no private reflection. Compile evidence does not establish native session privacy or shutdown.

## Chapter 29 — public transfer and cache publication

| ID | Risk | Current control | Remaining evidence |
| --- | --- | --- | --- |
| T18 | Appending the wrong representation or unbounded body | Trusted length/hash/strong ETag; exact 206 range; full 200 truncates; byte-count bound; verified promotion | Deployment proxy/CDN/TLS behavior and device storage checks pending |
| T19 | Server filename escapes cache or abandoned partials exhaust storage | Derived fixed-extension key; dedicated folder; complete+partial quota; free-space reserve; serialized explicit deletion | Physical low-space races, OS eviction and inventory/old-revision cleanup design pending |
| T20 | Private delivery leaks through anonymous cache or stale account operation | PrivateDelivery descriptor rejected; no credential attached; no private endpoint | Authenticated owner-scoped private delivery, lease/publication fencing, revocation/encryption/backup remain unimplemented |

Descriptor authority is supplied by teaching composition, not by a hash alone. Handler
injection is trusted; production redirects/decompression are disabled. The single-writer
contract is one instance per folder, not a cross-process lock. Same-directory rename
does not establish power-loss durability. Public sample data is not customer content.

## Chapter 30 — untrusted activation and push registration

| ID | Risk | Current control | Remaining evidence |
| --- | --- | --- | --- |
| T21 | Forged payload selects arbitrary route/account or exposes private content | Canonical typed grammar, bounded schema/expiry, fixed routes, no customer text, explicit request continuation plus existing API owner gates | Native callback/readiness and obsolete-page/session cancellation observations pending |
| T22 | Token registration crosses owners or leaks device endpoints | Dedicated scope, validated issuer/subject, fixed HTTPS origin/session lease, bounded token/body, redacted record text, no token echo, generation/owner conflicts | Real SDK token-validity/installation proof, provider configuration, durable cleanup/account orchestration pending |
| T23 | Replayed hints or retained private metadata outlive expected use | Real SQLite bounds (8 pending/128 retained), expiry/receipts, pending coalescing, explicit clear; only opaque synthetic request IDs | OS replay ordering, backup/shared-device privacy, crash between navigate/receipt; no exactly-once or physical-erasure claim |

Custom schemes are not verified domain associations. Push registry is single-process,
volatile and capped, with no outbound sender or provider credentials. Permission/shape
checks do not establish provider token ownership or delivery. Native lab acquires no
push token. Production needs SDK/account binding, durable cleanup and privacy review.

## Chapter 31 — untrusted references and file sharing

| ID | Risk | Current control | Remaining evidence |
| --- | --- | --- | --- |
| T24 | Provider path/type, oversized input or image metadata bypasses reference policy | Stream reads, generated paths, strict UTF-8 and byte/header/pixel/output bounds, native normalization port, RGB/RGBA PNG framing/CRC and ancillary removal | Native picker preprocessing/codec limits, orientation/colors/alpha and supported provider behavior pending; not full malware/decoder isolation |
| T25 | Shared URI exposes cache/AppData or retained copy is mistaken for revocable delivery | One narrow share root, verified generic copies, four-file/8 MiB budget, explicit cleanup and opportunistic 24-hour expiry | Actual Android URI/grants, iPad presentation, receiver lifetime and backup/privacy review pending; receiver copies cannot be revoked |
| T26 | Late selection attaches to another scope or logical cleanup is reported as erasure | Generation captured before picker; cancellation/commit fence; preserve prior on failure; immediate invalidation then separately confirmed cleanup | Durable authenticated owner/draft composition, orphan inventory, OS/picker cache recovery and physical-storage guarantees unimplemented |

The lab is transient unencrypted synthetic-reference storage, not customer upload. The
16 MiB aggregate staging budget includes abandoned app-owned scope files but not all
OS/provider/SDK caches. No camera or location feature is enabled. Clearing the local
workspace cannot erase the provider original, recovered picker result or receiver copy.

## Chapter 32 commerce boundary

| Threat | Control and remaining evidence |
| --- | --- |
| Forged owner/amount/state | Strict DTO; validated issuer/sub owner; server teaching pricing; no customer transition/payment route |
| Duplicate acceptance/lost reply | Owner/key + quote uniqueness; immediate SQLite order/history transaction; durable client operation before POST |
| Late reply crosses accounts/resources | Session leases; issuer/sub/origin/audience journal partitions; signed owner predicates |
| Pending request discarded after ambiguous error | Stable key retained except explicit post-replay definitive rejection; no automatic fresh submit |
| Private story in cache/logs | no-store, redacted payload ToString, bounded synthetic-only plaintext SQLite; physical protection/privacy lifecycle still pending |

See commerce-lab.md and verification.md. Controlled fixture state changes are not real
payment/administrative authorization. Capacity/rate limits are lab policy; distributed
abuse protection, backups and complete audit evidence remain operational work.

## Chapter 33 test payment boundary

| Threat | Control and remaining evidence |
| --- | --- |
| Browser/client forges paid or price | Empty-body owned checkout; immutable server amount; return/cancel pages never write state |
| Signature over normalized body | Exact bounded raw bytes, fresh timestamp, constant-time HMAC comparison; independent Python vector preserved by Git attribute |
| Replay/duplicate/order mismatch | Persistent scope/event facts and attempt/session/amount binding; atomic event/state transaction |
| Crash after provider session creation | Stable durable reservation/key, 23h automatic retry limit then reconciliation |
| Checkout used after cancellation | Block new/reused POST and suppress URL; previously obtained provider session may remain usable; late paid requires review |
| Cross-account private checkout disclosure | Validated owner predicates, no-store, current native lease; live device results pending |

Test environment only: no real-money authorization, refunds/disputes or financial ledger.
A valid unsupported event is acknowledged without effect, not financially reconciled.
Real provider/CLI delivery, complete audit/retention/recovery and store approval remain
pending. See ../payment-lab.md for precise contract and acceptance matrix.

## Chapter 34 tracking/private delivery boundary

| Threat | Control and remaining evidence |
| --- | --- |
| Foreign order/history/note disclosure | Validated issuer/sub parent lookup on list, tracking, revision and file paths |
| Lost/duplicate revision overwrites later review | Durable owner/resource/order key; replay before version checks; atomic revision/state/history/notice |
| Old descriptor or copied grant becomes access | JWT + scope + owner + current state/paid/version/revocation + hashed two-minute grant on every new read |
| Grant copied into URL/logs | Header-only credential, no-store and redacted record; production logging allowlist remains Chapter 35 work |
| Private file enters public cache or arbitrary path | Fixed embedded original fixture, generated IDs only, no static mapping, bounded full read; no private cache/player/export |
| Sign-out publishes old bytes/notes | Current session checks around HTTP and before UI, route/selection cancellation, original-owner journal retained |

Revocation cannot erase responses already authorized or copies already received.
Verified-buffer clearing is not physical erasure. The local fixture requires explicit
synthetic opt-in and direct development database authority; never expose its manufactured
TEST payment or graph methods as production administration. Real artifact ingestion,
provider delivery, native/iOS, privacy lifecycle and operational recovery remain pending.

## Chapter 35 staging/diagnostic/recovery boundary

| Threat | Control and remaining evidence |
| --- | --- |
| Teaching writes published as commercial authority | Operations rejects Production and Staging business-write flags; actual runner is isolated loopback |
| Private request/exception/scope enters logs | Enabled operations profile allowlists categories/typed fields, disables scopes and checks actual private canaries; new exporters require separate review |
| Customer IDs or URLs create unbounded metric series | Custom Meter uses bounded area/outcome only; correlation IDs belong in reviewed logs/traces |
| Restore revives old grant or loses replay receipts | New restore generation invalidates grants and preserves history/payment receipts; explicit hold/review before readiness |
| Live database replaced or unrelated schema modified | Explicit known-schema migration, online backup, integrity/hash checks and new restore directory; no live file swap |
| Backup hash mistaken for trusted recovery | Plaintext synthetic manifest checks bytes only; protected offsite copies, trusted integrity and external reconciliation remain operator work |
| Diagnostics/marketing consent silently reused | Local diagnostics default off; newsletter has independent consent and bounded confirmation port; no remote SDK/provider |

Current staging evidence does not establish public infrastructure, real financial/customer
reconciliation, native fatal-crash handling, encryption/erasure or complete production
telemetry privacy. Raw captures, databases and certificates are not CI artifacts. Existing
default-profile logging is unchanged; the allowlist applies only to enabled operations.

## Chapter 36 evidence boundary

The composed journey checks an original account's persisted intention and a foreign
account denial across API/journal/server reopen. This does not replace actual device
session, browser/MFA, hosted provider or private-media lifecycle observations.

The acceptance report accepts bounded UTF-8 results, rejects DTD/entity declarations,
validates per-run case IDs/counts/results and matches reviewed method selectors. Raw
XML/private failure output stays outside uploaded artifacts; static selectors, status,
counts, input hashes and source/run identifiers are projected. Runner console logs are
a separate channel, so these public suites must remain synthetic. A report hash does
not authenticate arbitrary inputs or establish a release attestation. Fresh same-job
execution supplies the association; manual checks stay not_run and release readiness
unassessed. See ../complete-application-testing.md for scope and limitations.
