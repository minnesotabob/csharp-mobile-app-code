# Chapter 34: tracking, revisions and private delivery

Opt-in Development lab with synthetic requests, simulated paid evidence and an original
three-second WAV. Not real production, customer audio upload, an administrative service
or a private offline player. Exact executable source and counts are in verification.md.

## Run and configure

1. Follow commerce-lab.md for identity/API origin and orders:read/orders:write. Use only
   invented stories/notes/contact data. Set Commerce__Enabled=true and
   Fulfillment__Enabled=true in Development. Default fulfillment routes are absent;
   non-Development mapping fails. Payments do not have to be enabled for tracking,
   but controlled publication requires the store's verified TEST Paid observation.
2. Run portable and API checks. FulfillmentApiTests contains a complete actual HTTP
   quote/accept -> controlled signed test payment -> paid production fixture -> review
   -> customer revision -> renewed review -> delivery -> authorized WAV read journey.
   It uses actual JWT middleware, production TrackingApiClient/coordinator and SQLite.
   The payment/production/publishing steps are controlled fixtures, not real services.
3. Open Home > Order tracking and delivery lab. Sign in through configured SDK identity,
   explicitly refresh the first page, select an order and load history/notices. Earlier
   Chapter 16 process-only submissions do not create these server orders.
4. Use a disposable development fixture to call PublishReviewForLab after the paid
   production gate. Submit a synthetic revision from ReadyForReview. Refresh to observe
   InProduction, the incremented order version and retained revision/history/notice.
5. Publish another review, then PublishDeliveryForLab. These are trusted in-process
   fixture calls: no admin/publication HTTP endpoint is supplied. The UI observes the
   Delivered notice on its next explicit refresh. It is an in-app feed, not APNs/FCM.
6. Fetch and verify delivered sample audio. The client obtains a two-minute grant and
   reads one complete 96044-byte WAV into bounded memory, validates exact length/hash,
   then clears the owned verified array. The native page reports verification; it does
   not play, export or cache the private bytes. Native identity/TLS/results are pending.
7. Exercise expiry, fresh grant issuance, wrong owner, wrong delivery ID, stale order
   version and RevokeDeliveryForLab. Revocation blocks future requests; it cannot erase
   bytes already returned. The three-second tone is an original public-source teaching
   artifact, not a secret customer recording. Its protected route demonstrates policy.

No fixture administrator is automatically run by the server. A runnable local console
fixture is supplied for staging the native lab without writing another harness. Its
smoke mode uses a temporary database and is executed in CI:

```powershell
dotnet run --project tools/GnazzosWorld.FulfillmentLab -c Release -- smoke
```

For a disposable native/API lab, set the same absolute database path in the server and
fixture terminals. Use the actual validated issuer/subject for your development account;
an email address is not the subject. Quote provider subjects containing a pipe character.
The explicit fixture switch permits direct synthetic database writes and manufactured
TEST payment evidence. It must never be set for customer/production data.

```powershell
$env:GNAZZO_SYNTHETIC_FIXTURES = "1"
$env:Commerce__Database = Join-Path $PWD "lab-data/synthetic-fulfillment.sqlite3"
$lab = "tools/GnazzosWorld.FulfillmentLab"
dotnet run --project $lab -c Release -- seed-review $env:Commerce__Database "https://your-tenant.auth0.com/" "your-validated-subject"
# Copy only the printed synthetic order ID; after the customer's revision reaches v4:
dotnet run --project $lab -c Release -- review $env:Commerce__Database "ORDER_ID" 4
dotnet run --project $lab -c Release -- deliver $env:Commerce__Database "ORDER_ID" 5
# Delivery is v6; optional availability revocation:
dotnet run --project $lab -c Release -- revoke $env:Commerce__Database "ORDER_ID" "DELIVERY_ID" 6
```

These are local trusted developer fixture commands, not authenticated administrative
endpoints. They print synthetic IDs/versions and never print keys, grants or notes.
Creating a real customer/admin workflow, artifact ingestion and production authorization
requires additional work. Never expose Chapter 32 AdvanceForLab as a payment bypass.

## HTTP contract

| Route under /api/v1/commerce | Scope | Purpose |
| --- | --- | --- |
| GET /orders?after={canonical-id} | orders:read | Up to 20 owned summaries, optional next cursor |
| GET /orders/{id}/tracking | orders:read | One coherent bounded owned snapshot |
| POST /orders/{id}/revisions | orders:write | ExpectedVersion, OperationKey, Note |
| POST /orders/{id}/deliveries/{delivery}/grant | orders:read | ExpectedVersion -> short-lived grant |
| GET /orders/{id}/deliveries/{delivery}/content | orders:read + X-Delivery-Grant | Authorized complete sample WAV |

JWT middleware validates the credential; every store lookup filters validated issuer/sub.
A foreign or unknown object is unavailable. IDs, descriptors and notices grant no access.
All commerce responses use no-store. File responses also use nosniff and a fixed download
name. No caller-controlled path, static private-file directory or remote content URL is
accepted. Query grants are rejected, and the sample explicitly rejects Range requests.
The earlier public cache/downloader remains unable to accept private delivery descriptors.

List pages use live ascending canonical-ID keysets, not a frozen multi-page snapshot.
Newly inserted random IDs before the cursor may require refreshing the first page.
The tracking read uses one SQLite read transaction across order/history/revision/
delivery/notice queries. It caps history/notices at 100, revision receipts at 10 and
current delivery at one; over-cap data fails rather than appearing silently truncated.
List rows omit stories/notes. Private detail text is read only after an owned lookup.

## Durable customer revision

A revision is accepted only for an owned ReadyForReview order with matching expected
version and verified TEST Paid state. Normalize the nonempty note (<=1000 UTF-16 chars,
no unsupported controls). Strict JSON rejects authority fields and duplicate properties;
body bound is 16384 bytes. The note never changes the immutable original request/price.

An immediate server transaction looks for the owned order/key replay first. Matching
base version + canonical note returns the original immutable receipt even after later
versions. Reusing a key for different facts conflicts. For a new operation, check current
version/state, then insert revision + advance to InProduction + append history + insert
notice atomically. At most 10 revision requests per order; publication paths enforce a
100-history budget. Actual writers, not an in-process lock alone, serialize via SQLite.

PendingRevisionDatabase persists the operation before POST. Partition: issuer, subject,
API origin, audience, order ID. One last row per partition/order; at most 64 rows in a
separate plaintext synthetic SQLite file. An unresolved operation cannot be overwritten.
A matching confirmed receipt is saved before UI success. Same-key replay survives lost
reply, server/client reopen and a subsequent review publication.

Only post-replay VersionConflict/InvalidTransition/Unavailable responses can complete
an unresolved attempt as rejected. Generic invalid input, key conflict, capacity,
transport error or unknown outcome retain uncertainty and the original key. Receipt
history is immutable; it is not a newer tracking snapshot. Refresh tracking after a
revision rather than forcing its older applied version into current order state.

Sign-out clears current UI and blocks late lease publication but retains original-owner
pending rows for reconciliation. Unsubmitted editor text is not durable and is cleared
on selection/departure. Privacy export/deletion composition for these new records remains
pending; sign-out is not physical erasure. Never put access tokens or grants in this journal.

## Delivery and notices

Controlled review/publication functions require matching versions, legal current states
and verified TEST Paid evidence. Publication commits one immutable delivery descriptor,
Delivered state/history and a content-free notice in the same transaction. Exact fixture
replay returns the same descriptor and cannot undo revocation. RevokeDeliveryForLab
marks the descriptor revoked, invalidates grants, advances the order version/history
(with the same Delivered state) and emits DeliveryRevoked atomically. This is logical
availability revocation, not deletion of the original sample or copies already read.

Notices contain generated ID, kind, order version and timestamp; no note/story, token
or download URL. They are durable owned in-app observations, not delivered emails or
provider push receipts. A future outbox can distribute generic hints through Chapter 30's
provider boundary; receipt/acknowledgment semantics and live push remain separate.

A grant contains 32 random bytes encoded as 64 lowercase hex characters. Store only its
SHA-256 hash, delivery ID, owner, current order version and server expiry. At most four
active grants per delivery and 1000 globally; issue removes expired rows. Grant lifetime
is two minutes; exact expiry rejects. Version/revocation/paid/state checks run again at
file authorization. Every read still requires a valid JWT and owner match; the grant is
not a bearer-only URL and cannot be moved to another order/account/delivery.

Authorization is evaluated when the read starts. Expiry or revocation cannot retract a
response already authorized or erase received bytes. The client also fences current
session before/after HTTP and before UI publication. It never sends the credential to a
redirect or arbitrary descriptor URL. These controls do not prove physical erasure,
continuous streaming reauthorization or a complete production rights-management system.

## Client and original audio

TrackingApiClient fixes HTTPS origin, disables redirects and bounds the entire operation
with deadlines. JSON <=256 KiB; errors <=8 KiB; requests <=16 KiB. Fetch has one 20-second
whole-operation budget across grant and content. It validates IDs, coherent versioned
history, descriptor, fresh grant, status/media type, exact bytes and SHA-256. It requests
no Range and has no private cache, local filename, share target or background resume.

VerifiedDelivery.Dispose clears its owned byte array. Managed transport/serializer/OS
copies may exist; this is not a physical-erasure claim. The sample hash is fixed for the
lab and must not become authority for arbitrary customer files. Production needs trusted
artifact ingestion/metadata, key management, per-owner storage, scanning/format policy,
retention/backups and supported playback/storage composition.

eng/generate_private_delivery.py creates the original three-second 16-kHz mono PCM tone.
API embeds it as PrivateDeliverySample.wav and validates its length/hash at mapping.
The app does not bundle it as a public native media resource. Source code and generated
teaching fixture are public artifacts; access tests establish route policy, not secrecy
of these known bytes. No artist recording or copyrighted customer work is used.

## Acceptance matrix

| Scenario | Current evidence / required observation |
| --- | --- |
| Owned list/tracking snapshots, history and notices | Real temporary SQLite + signed JWT API tests |
| Revision key replay, stale version, invalid state/owner, limits | Real SQLite and actual API tests |
| Failed notice insert rolls back revision/publication | SQLite trigger failure tests |
| Concurrent revision retry | Two independent SQLite connections |
| Lost revision reply, both databases reopen, later review | Production coordinator with simulated HTTP loss |
| Journal failure before POST and sign-out late result | Portable controlled persistence/HTTP tests |
| Quote -> test paid -> review -> revision -> delivery -> exact WAV | Actual API/production client integration; controlled service fixture steps |
| Anonymous/foreign/missing/expired/revoked/wrong-delivery access | Signed in-process file-route integration |
| Corrupt/oversized/wrong media bytes, buffer disposal | Portable client policy with exact original sample |
| Native list/keyboard/accessibility/lifecycle/TLS | Pending device execution |
| Native private listening/offline/export | Not implemented by this lab; explicit composition work |
| Real customer/admin/upload, push/email delivery | Not implemented; service/provider integration pending |
| Real payment/production, privacy lifecycle, audit/backups | Separate gates; Chapter 35 operational work |

Record revision, platform/provider, precise steps, observed result and sanitized evidence.
Do not screenshot real notes, credentials or grants. A native compile is not a device
read/listening result. Current reference boundaries: ASP.NET Core resource authorization
https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resource-based;
SQLite transactions https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions.
