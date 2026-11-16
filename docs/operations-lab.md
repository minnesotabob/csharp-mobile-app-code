# Chapter 35: isolated staging, safe diagnostics and recovery

This is an isolated synthetic operations lab. It publishes the API and runs an actual
HTTPS Staging process on loopback, with a migrated SQLite database and a validated
original audio fixture. It does not provision a public cloud host, enable real customer
writes, send newsletters or configure a remote crash/telemetry account. See verification.md
for exact source, run IDs and observed results; unexecuted device/provider work stays pending.

## Run the published staging/recovery drill

Use SDK 11.0.100 from the repository root. No mobile workload or provider credentials
are needed for this runner. Choose a new evidence filename on each run:

```powershell
dotnet publish src/GnazzosWorld.Api -c Release -o artifacts/api
dotnet build tools/GnazzosWorld.OperationsLab -c Release
dotnet run --project tools/GnazzosWorld.OperationsLab -c Release --no-build -- staging-smoke artifacts/api/GnazzosWorld.Api.dll artifacts/api/Fixtures/delivery_sample.wav artifacts/operations-evidence.json
```

The runner creates its own temporary data and ephemeral localhost certificate. Its
client accepts only the exact generated certificate, correct loopback name and fixed
origin; it rejects other TLS errors and redirects. It installs no machine trust root
and uses no global certificate-validation bypass. This narrow test pin is not a public
production certificate policy. Password/private-key material stays in the temporary
process configuration and is never included in evidence. The owned process/workspace
is stopped/removed after the run. The evidence JSON is an allowlisted public-safe summary.

The actual sequence:

1. Initialize schema 1, create a synthetic accepted request and apply a locally signed
   TEST payment fixture. Start controlled production, review, delivery and a read grant.
2. Use SQLite's online backup while the original store remains open. Mutate the source
   after the snapshot so recovery cannot accidentally pass by reading the live database.
3. Restore into a new directory, invalidate restored delivery grants and retain a new
   restore generation with restore_pending=1. Preserve orders, history, revisions and
   payment-event replay evidence. No running database is overwritten.
4. Publish/run the actual API DLL under Staging with synthetic business routes disabled.
   Check HTTPS liveness=200 and readiness=503 while restore review is held.
5. Verify restored business state, original paid-event replay and invalidated grant.
   Release only the expected restore generation through the local operator method;
   readiness must become 200 while the process stays running.
6. Send a bounded controlled failure with private canaries in URL, headers and body.
   Require safe 503 response, a server-generated operation ID and a matching 3502
   trace record. API tests separately verify the 3501 completion fields. Raw captured output must contain none of the canaries, the
   exception message, certificate password or old grant. Liveness must remain 200.
7. Write only bounded evidence: deployment/schema/recovery outcomes and generated
   operation/trace IDs. No database, raw logs, request fields, certificate or credentials
   are uploaded by the operations workflow; evidence retention is seven days.

This is a real published-process HTTPS/recovery exercise. Its payment/production data
is manufactured by trusted local fixtures, not by Stripe or real administration. The
controlled exception is recoverable; it is not an actual managed/native process crash.

## Configuration profile

All operations endpoints are absent unless Operations__Enabled=true. The profile permits
Development or isolated Staging and rejects Production. In non-Development it rejects
Commerce__Enabled, Payments__Enabled or Fulfillment__Enabled. Existing synthetic write/
checkout/delivery labs remain Development-only boundaries; publishing a DLL does not
turn them into a production commerce service.

| Setting | Meaning |
| --- | --- |
| Commerce__Database | Absolute migrated synthetic SQLite path; readiness uses read-only access |
| Operations__ArtifactsRoot | Absolute private artifact directory, containing delivery_sample.wav |
| Operations__TracePercent | Integer 0–100, default 10; bounded head sampling for ordinary traces |
| Operations__FailureProbe | Explicit controlled failure endpoint, off unless enabled |
| ASPNETCORE_ENVIRONMENT and DOTNET_ENVIRONMENT | Same Staging value in the runner to avoid precedence surprises |
| Kestrel certificate path/password | Ephemeral runner-only HTTPS settings; no source-code secret |

The API never exposes the artifact directory as static files. Readiness checks the
known file's length/hash and schema/maintenance state, not the customer's ability to
pay or a provider's availability. /health/live proves process responsiveness;
/health/ready proves only these defined local dependencies. Both return generic status
and no-store. The existing /health public fixture contract remains available.
Failure probe accepts <=1024 bytes, has a shared 10/minute budget and returns no raw
exception detail. Keep operations health/probes on a private staging network. Host
binding, firewall, trusted proxy/forwarded headers, public DNS/cert renewal and production
provisioning require a separate reviewed deployment. No automatic public endpoint exists.

## Explicit schema and backup/restore commands

Direct operator commands require a disposable synthetic path and explicit local opt-in.
They are not authenticated HTTP administration. Never target real customer data:

```powershell
$env:GNAZZO_SYNTHETIC_FIXTURES = "1"
$ops = "tools/GnazzosWorld.OperationsLab"
dotnet run --project $ops -c Release -- init "ABSOLUTE_NEW_DB_PATH"
# Existing complete Chapter 32–34 version-0 commerce database:
dotnet run --project $ops -c Release -- migrate "ABSOLUTE_LEGACY_DB_PATH"
dotnet run --project $ops -c Release -- backup "ABSOLUTE_DB_PATH" "NEW_BACKUP_DIRECTORY"
dotnet run --project $ops -c Release -- restore "BACKUP_DIRECTORY" "NEW_RESTORE_DIRECTORY"
# Only after reviewing restored state/provider reconciliation; use the printed generation:
dotnet run --project $ops -c Release -- release-restored "RESTORED_DIRECTORY/commerce.sqlite3" "EXPECTED_GENERATION"
```

Initialization never overwrites an existing file. The explicit version-0 -> 1 migration
validates the known commerce table set, adds migration/maintenance records in one
transaction and sets user_version=1. Repeating it is safe; unrelated or newer schemas
are rejected. The normal store refuses future versions before its CREATE statements.
Opening an older development store does not silently claim an operations migration.

Backup uses SqliteConnection.BackupDatabase, not File.Copy on an active database.
It validates a bounded database, relationships and manifest hash, and publishes database
plus manifest by renaming a new sibling directory. This synchronous API can block writers;
measure/plan its window and I/O. The 64-MiB lab file policy and busy timeout are not a hard
wall-clock or platform-allocation guarantee. Quiesce a real write workload when required.

The manifest has format version, database version, byte length, SHA-256 and timestamp;
it contains no source path/customer fields. A hash detects change relative to this
manifest, not malicious replacement of both data and manifest. Trusted backup storage,
encryption, key custody, signatures/access controls and off-site retention remain operator
responsibilities. The lab backups are plaintext and synthetic. Do not upload databases
or raw logs as public CI artifacts.

Restore verifies the fixed manifest/database names, hash, schema and relationships,
then rechecks copied bytes before publishing a new directory. It deletes delivery-grant
rows and writes a new pending generation. It preserves replay receipts: deleting those
could turn a retry into a new payment/order operation. Old client journals are not part
of this server backup. A restore can roll history backward; hold writers, reconcile
external provider facts and outstanding client operations before any production release.

release-restored is an explicit local lab acknowledgment, not proof that all external
financial/customer state was recovered. There is no live-file swap, automatic traffic
cutover, production rollback or disaster-recovery service. Define RPO/RTO, backup cadence,
retention, restore owners and artifact inventories before deployment. The fixture WAV is
an immutable release artifact; real uploaded blobs need their own versioned inventory,
checksums, protected storage and coordinated backup/restore plan.

## Diagnostic projection and evidence

Operations mode configures JSON console logging with inherited scopes disabled and
restricts categories to the safe operations projection plus the earlier typed owned-read
decision audit. It suppresses ordinary framework request/authentication/exception output,
which can otherwise include URLs, query strings, scopes or exception messages. Adding
an exporter/provider/filter requires re-running redaction tests against actual output.

| Event ID | Stable template fields |
| --- | --- |
| 2601 | Earlier allowlisted owned-read decision |
| 3501 | OperationId, Area, Outcome, StatusClass, DurationBucket |
| 3502 | OperationId, TraceId, Area, Outcome |

OperationId is generated by the server; a caller's X-Correlation-Id is ignored. Standard
W3C trace context may be propagated, but is bounded correlation data, not identity or
authority. The custom ActivitySource exports only explicit generated ID/trace/enum facts;
it does not serialize Activity, baggage, full scopes or exception objects. The controlled
failure trace is always sampled; ordinary traces use the configured head percentage.
Completion logs and low-cardinality counters/histograms remain distinct from sampled
traces. Metric tags are only bounded area/outcome values: no customer/order IDs or URLs.

The lab exports custom ActivitySource/Meter/ILogger projections locally, with no remote
account. OpenTelemetry is the maintained server collection/export path selected from
current official documentation; add only reviewed sources/exporters, credential handling,
queue limits and redaction before OTLP deployment. Automatic ASP.NET/HttpClient sources
can carry additional URL/peer fields and are not enabled by this sample. A general
collector cannot be assumed private because a custom span was safe.

Do not log Authorization, cookies, delivery grants, checkout links, webhook signatures,
raw request/response bodies, notes/stories, email, SQL parameter values or arbitrary
exception text. A structured template does not scrub sensitive values placed in it.
Stable event IDs help queries; they do not create an immutable financial/audit ledger.
Logging filter suppression also removes detail, so diagnosis needs safe failure codes,
controlled reproduction, protected break-glass procedures and release/symbol information.

## Mobile/server failures, privacy and maintained SDK choice

The native Diagnostics and consent lab defaults off, persists a separate local consent
choice and retains at most 20 typed events in memory. It accepts enum area/fault values,
generated event ID and time, never Exception or free text. Turning collection off clears
this buffer. It does not initialize a remote SDK, attach screenshots or capture a real
crash. Buffer clearing is logical retention control, not physical erasure of managed/OS
copies. Local collection consent is not blanket consent for remote export or newsletter.

Sentry.Maui is the maintained mobile managed/native crash-reporting integration selected
for a separately configured deployment; current documentation also describes native
Android/iOS handling, release symbols, breadcrumbs and session/device metadata. Its
quickstart enables broad diagnostic options for demonstration. Do not copy those into
this private customer app without review. Keep default PII off, review messages, breadcrumbs,
attachments/screenshots, request context and identifiers, and use client-side scrubbing/
dropping before transmission. Review native-crash payload handling separately from
managed BeforeSend hooks; one managed test does not establish native crash privacy.

Test SDK initialization/consent ordering, offline persistence, retry after restart,
retention/deletion, symbol upload and actual isolated Release crashes on named devices.
A caught storage failure is different from an unhandled managed fault, native abort,
watchdog termination or OS process death. Server fatal crashes/dumps can bypass normal
middleware/logger projection and may contain secrets; restrict access, lifetime and
export independently. No Sentry DSN, remote crash export or device-crash result is claimed
here. Package/OS compatibility must be pinned and verified when actually integrated.

## Newsletter boundary

NewsletterBoundary requires separate explicit consent, a stable canonical operation key,
a bounded validated address and consent version newsletter-v1. It asks an INewsletterGateway
for confirmation within a ten-second caller budget. PendingConfirmation is not Subscribed;
network uncertainty retains an unresolved result, and a timeout cannot prove the provider
did nothing. A real gateway must honor stable-key replay, TLS, redirect/body/response
bounds, double opt-in, suppression/unsubscribe and provider-specific secrets. No provider
or outgoing message is configured. Never reuse song-request contact fields automatically.

## Operational acceptance and cost worksheet

| Concern | Executed lab / separate production decision |
| --- | --- |
| Publish/run Staging over real HTTPS | Loopback child process with narrow ephemeral-cert pin |
| Schema/online backup/new-directory restore | Real SQLite, WAL/snapshot tests, hold/release and grant invalidation |
| Safe correlated controlled failure | Actual HTTPS request + generic response + 3501/3502 output; raw canaries absent |
| Consent/bounds/newsletter uncertainty | Portable tests, native consent page compilation; no remote exports/messages |
| Public hosting/DNS/TLS renewal/proxies | Separate infrastructure selection, permissions and deployment |
| Native/server fatal crash collection | Candidate integrations documented; configured SDK/device/dump evidence pending |
| RPO/RTO, encrypted off-site backups, artifact inventory | Operator plan and restore rehearsal required |
| Provider/payment reconciliation after restore | Manual controlled evidence only; complete production procedure pending |

Budget compute/concurrency, database and artifact storage, backup copies, private egress,
provider requests/webhook retries and telemetry ingestion/retention. Bound queues and
labels, sample traces deliberately and set alerts before raising limits. Storage quotas
and test timings do not establish production traffic capacity or cloud prices. Check
current provider pricing for the actual region/retention/load before provisioning.

Official references checked November 16, 2026:
- https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/backup
- https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel
- https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs
- https://learn.microsoft.com/en-us/aspnet/core/fundamentals/environments
- https://docs.sentry.io/platforms/dotnet/guides/maui/
- https://docs.sentry.io/platforms/dotnet/guides/maui/data-management/sensitive-data/
