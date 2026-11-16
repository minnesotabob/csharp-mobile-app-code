# Chapter 32: quotes, orders and customer ownership

Opt-in synthetic teaching workflow. Accepted is a recorded request, not payment,
real production, a live price offer or attachment upload. Use invented contact/story
fields only. Current executed revisions and run evidence are in verification.md.

## Configure deliberately

1. Follow the existing API identity configuration in labs/ch23-sessions-api.md. The API
   must validate the configured HTTPS JWT authority, audience, signature and lifetime.
2. Grant development API permissions `orders:read` and `orders:write` through the
   identity provider. Existing `requests:read` preview permission is insufficient.
3. Start a disposable development server with `Commerce__Enabled=true`; optionally
   set `Commerce__Database` to a writable private development path. Default is
   `AppContext.BaseDirectory/lab-data/commerce-v1.sqlite3`. Disabled routes are absent.
   Do not commit databases or use real customer data. This separate v1 file has no
   schema migration/backup policy yet.
4. Configure native AuthenticationConfiguration and the fixed HTTPS API origin using
   the earlier session lab. Set `EnableOrderLabScopes=true` only after provider grants
   exist. This flag requests scopes; it cannot grant them. It defaults false.
5. Use a trusted development certificate reachable from the device. Never disable
   validation. A phone's localhost is the phone, not the development computer.
6. Open Home > Quote and order lab. Sign in; choose a synthetic sample or explicitly
   send the earlier saved synthetic draft. Review all returned fields/amount/expiry,
   then accept. The earlier draft is read only; local attachments remain separate.

API identity is unconfigured by default. These steps are an acceptance procedure,
not evidence that a live tenant or device completed them.

## Contract

| Operation | Permission | Body/result |
| --- | --- | --- |
| POST /api/v1/commerce/quotes | orders:write | QuoteCommand(Request); OrderQuote |
| POST /api/v1/commerce/orders | orders:write | AcceptOrderCommand(QuoteId,OperationKey,Request); OrderSnapshot |
| GET /api/v1/commerce/orders/{id} | orders:read | owned OrderSnapshot |

Owner comes only from validated issuer/sub claims. No owner, amount, state or paid
field is accepted from the customer. Unknown/foreign records are unavailable (404).
JSON unknown properties, duplicate object fields, null/invalid specifications and
bodies >16384 bytes are rejected. Canonical IDs are lowercase 32-character GUID N.
All commerce responses are no-store; process-local fixed window permits 20 ops/min,
no queue. This is a lab budget, not distributed/fair per-user rate protection.

The server validates/normalizes SongRequest, hashes `request-v1\n` + canonical JSON,
calculates its own historical teaching price, and persists USD integer minor units,
pricing-policy version, request/hash and a 15-minute server expiry. Hashing does not
encrypt private content. USD-only minor units do not imply a general currency model.
Quotes are capped at 100 per owner and 1000 total, including expired/used history;
there is no automatic retention cleanup. Reaching a cap is a lab capacity outcome.

Acceptance uses an immediate SQLite transaction and parameterized SQL:

1. Look up validated owner + operation key. Matching quote/hash returns the existing
   current order even after expiry; different quote/hash is KeyConflict.
2. Only for a new operation, load the owned quote, compare normalized request/hash,
   enforce expiry and reject an already-used quote.
3. Insert immutable order snapshot + initial version-1 Accepted history and commit.
   Unique `(issuer,subject,operation_key)` and unique `quote_id` independently guard
   duplicate operations and different-key reuse. Failed history insert rolls back all.

This is an application POST replay protocol, not exactly-once transport or implicit
HTTP POST idempotency. SQLite serializes independent writers; the instance lock alone
does not provide that guarantee. Lock timeout or lost response remains uncertain.

## Client reconciliation and identity

AcquireOwnedAsync captures current lease + validated owner under the session gate.
Before acceptance HTTP, PendingOrderDatabase persists the quote + stable generated
key. Partition: issuer, subject, API origin, audience. No token is saved. One last
operation/receipt per partition, at most 16 partitions; unresolved work cannot be
overwritten by a new quote. Storage is plaintext synthetic AppData SQLite. No full
order collection, privacy export/deletion composition or automatic startup POST exists.

OrderApiClient uses fixed HTTPS origin, no redirects, before/after lease checks,
16384-byte request, 32768-byte success and 8192-byte error bounds. It validates receipt
ID/key/quote/hash/request/money/state/version and ignores raw server error messages.
The coordinator persists confirmation before the UI announces success. A later refresh
uses GET and cannot downgrade a confirmed receipt or local version.

A pending ExpiredQuote/ChangedRequest/Unavailable rejection can be recorded definitively
because the service emits these after replay lookup. Generic InvalidRequest, unknown
error, transport failure, KeyConflict and QuoteUsed retain uncertainty and the original
key. Never start a fresh acceptance merely because its prior reply is missing.

Sign-out cancels current work and clears displayed quote, but retains original owner
journal for later reconciliation. Late A replies cannot publish under B; B cannot
load A's partition. Actual Shell departure/window destruction cancels page work;
auth browser visibility alone does not. Cancellation cannot reverse server commit.

## Controlled states

Accepted -> InProduction or Canceled; InProduction -> ReadyForReview;
ReadyForReview -> InProduction or Delivered. Delivered/Canceled terminal.
AdvanceForLab is a trusted in-process fixture ONLY, with expected version + atomic
history. No customer status/payment update route or production administrative service
is exposed. Fixture InProduction is simulation; Chapter 33 must establish verified
payment/production authorization before any real workflow. History is not a complete
actor/rationale/provider-event audit trail. High availability, backups and recovery
remain Chapter 35 work.

## Acceptance worksheet

Record revision, device/OS, provider setup, scenario, expected/observed result and
sanitized evidence. Never record credentials or real stories. CI is not device evidence.

| Scenario | Current evidence / required observation |
| --- | --- |
| Price/normalization/immutable snapshots | Portable SQLite tests; native review pending |
| Same-key duplicate and two-connection race | Portable real SQLite; one order/history |
| Different-key quote reuse, payload conflict | Portable + authenticated API tests |
| Failure between order/history insert | Real SQLite trigger rollback test |
| Lost acceptance reply + both databases reopened + expired quote | Deterministic production coordinator with simulated HTTP failure; original order returned |
| JWT scope/ownership/no-store/strict JSON | Signed-token in-process API integration tests |
| Actual OrderApiClient through signed API middleware | In-process integration create/accept/persist/fixture advance/GET refresh |
| A signs out before reply; B partition | Portable controlled late response + journal tests; native pending |
| Bad request/unknown failure retains pending key | Portable classification policy; native recovery pending |
| Stale/invalid/terminal transitions | Portable controlled fixture; no live/admin claim |
| Live provider grant/sign-in/refresh/sign-out | Pending configured tenant/device |
| Real device TLS/restart/storage failure | Pending Android and iOS |
| Page departure/auth browser/window destruction | Native observation pending |
| iOS compilation, real payment/production/delivery | Pending separate platform and later chapters |

Run the portable/API workflows; then complete device rows independently. A default
server 404 means routes may be disabled, not necessarily a missing owned order.
Read verification.md for exact source/run counts and any setup warnings.
