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
