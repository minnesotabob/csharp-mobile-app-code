# Chapter 33: hosted test checkout and verified events

Development-only, opt-in, Stripe direct-account TEST mode. No live keys, real cards,
real money, Connect, discounts, taxes, refunds/disputes or production authorization.
The app never launches checkout. It can explicitly refresh an owned server TEST payment
status after reconciling its last accepted request. Use synthetic stories/contact only.

## Evidence and prerequisites

verification.md records exact source/run evidence. Automated integration reconciles a
signed synthetic test payment through real JWT middleware, production endpoint/verifier
and real SQLite. Provider HTTP responses are simulated. A Python-generated signature
vector independently checks exact bytes. No live Stripe sandbox/CLI/card interaction,
native identity/TLS/storage behavior, iOS build or store approval is claimed.

The real provider adapter is supplied for a separately configured developer sandbox.
It sends HTTPS requests only to api.stripe.com, rejects redirects, verifies account ID,
uses an explicit 20-second whole-operation deadline and caps JSON replies at 64 KiB.
The API contract is pinned to 2026-10-28.endive; configure the snapshot webhook endpoint
for that contract and rerun adapter/schema tests before upgrading.

## Configure a provider sandbox

1. Follow commerce-lab.md to accept a synthetic order with dedicated orders:read/write
   permissions. Note its canonical order ID. The earlier in-memory Chapter 16 simulator
   does not create a server order and cannot participate.
2. Create/select a Stripe sandbox/test environment and inspect its account identifier.
   Obtain its secret test key locally. Never paste keys into source, screenshots or
   shell history. Use environment/secret tooling outside Git. No key is shipped here.
3. Run the API in Development with Commerce__Enabled=true and Payments__Enabled=true.
   Configure Payments__TestSecretKey (must begin sk_test_), Payments__AccountId (acct_),
   Payments__WebhookSecret (whsec_) and Payments__ReturnOrigin (canonical HTTPS origin
   ending /). ReturnOrigin should reach this API's fixed /payment-test/return and
   /payment-test/cancel pages. Invalid configuration fails when payment routes map.
4. Use the official Stripe CLI's local listener or a configured HTTPS snapshot webhook
   destination. Forward the four supported checkout session event types to
   /api/v1/commerce/test-payments/webhook. The CLI listener signing secret is different
   from a dashboard destination's secret; use the secret for the actual sender.
   Request only needed event types and use the documented API contract/version.
5. From a developer desktop/browser workflow outside the distributed mobile app, issue
   an authenticated empty-body POST to /api/v1/commerce/orders/{id}/test-checkout.
   Keep the returned URL private. It is a hosted checkout capability and belongs to
   that owner; no story or email is sent by our adapter. The hosted provider may collect
   its own test contact details. Open only the validated checkout.stripe.com test URL.
6. Complete hosted checkout using the provider's documented TEST payment details.
   Never use a real card. Observe the signed event and read the owned
   /api/v1/commerce/orders/{id}/test-payment response. Require Paid from the server;
   a browser return or a dashboard screenshot alone is not our reconciliation evidence.
7. Cancel a separate fresh order's checkout and forge return query values such as
   ?paid=true. The fixed return pages never write payment state. Redeliver an event,
   send completed/unpaid before async success and test failure/expiry after Paid.
8. Record account environment, API/webhook version, source revision, event type/ID,
   sanitized server receipt and exact observed result. Do not include secret keys,
   access tokens, private checkout URLs or customer content. Leave rows pending until
   actually executed. Real refunds/disputes need a separate implementation before use.

Do not create a real charge, deploy a public checkout or change storefront behavior
as part of this lab. No credentials or account provisioning are required for CI tests.

## HTTP and trust contract

| Operation | Gate | Effect |
| --- | --- | --- |
| POST /api/v1/commerce/orders/{id}/test-checkout | JWT orders:write + owned Accepted order | Reserve/reuse attempt, create/attach one hosted test session |
| GET /api/v1/commerce/orders/{id}/test-payment | JWT orders:read + owner predicate | Read test receipt, private URL only while Pending |
| POST /api/v1/commerce/test-payments/webhook | Bounded fresh raw-body signature; no customer JWT | Apply verified test checkout facts atomically |
| GET /payment-test/return or /cancel | No authority | Static no-store information, no database write |

The checkout POST accepts no body: no amount, paid flag, card, owner or return URL.
Quote/order snapshots supply integer USD amount. A durable reservation captures one
attempt ID, scope (stripe-test:account), amount/currency, creation time and fixed return
origin. Provider calls happen outside SQLite transactions. The server's attempt ID
forms the provider idempotency key. Same order retries reuse it. Because provider v1
keys may be removed after at least 24h, automatic creation retries stop at 23h and
require operator reconciliation. A Creating attempt is never replaced with a new key.
Changing account/return-origin configuration also blocks automatic reuse.

A crash after provider creation but before local attachment leaves Creating. The same
reservation/key can recover within the retry window. If a webhook beats attachment,
503 preserves provider retry rather than marking an unbound event handled. The pending
attempt must be reconciled before retiring keys/evidence or resetting the lab.

## Verification and transaction

Webhook raw bytes <=65536; signature header <=2048; exactly one timestamp; <=300-second
past/future skew. Verify HMAC-SHA256 over `timestamp.rawBytes`, accepting a matching v1
signature using constant-time comparison before JSON parsing. Multiple v1 entries
support overlapping signatures during rotation; the service currently holds one active
endpoint secret. Secret rollout/retirement and delivery recovery remain operational work.

Bounded JSON rejects duplicate object properties and excessive depth. Require test
mode, direct-account event (no Connect account), checkout.session, payment mode, known
session/attempt/order, USD amount matching the reservation and a paid payment reference.
Recognized events: checkout.session.completed, async_payment_succeeded,
async_payment_failed and expired. Completed/unpaid remains Pending. Unsupported signed
events are acknowledged without effect; refunds/disputes are not implemented and must
not be mistaken for handled financial lifecycle.

Persistent event key: (scope,event_id), with a fingerprint of the verified payment facts.
A duplicate with the same facts is acknowledged; same ID/different facts conflicts.
Different event IDs for the same paid session also have no duplicate state effect.
In one immediate transaction, insert the event receipt and update payment state/version.
Failure rolls both back. A 10000-event global lab cap fails for operator reconciliation;
there is no automatic eviction or production retention policy.

Paid never downgrades due to late unpaid/failure/expiry. Verified success can resolve an
earlier failure. Canceled orders cannot create/reuse checkout through the POST and no longer expose the
URL through owned reads. Cancellation does not expire a provider session already opened;
a previously obtained URL may still be usable. Paid arriving after cancellation becomes
ReviewRequired and
cannot start the controlled production fixture. The payment version is separate from
the order version. StartProductionForPaymentLab checks Paid + Accepted + expected order
version in one transaction. Chapter 32's AdvanceForLab remains a legacy graph-only test
fixture; never expose it as production administration or wire it to real work.

Payment state says only what the supported signed test facts establish. No automatic
fulfillment, email, refund or payout is performed. No success page can mutate this state.
Reconciliation GET proves ownership/current stored observation, not a fresh provider
balance query. Missing/delayed/unsupported events need provider-side investigation;
there is no polling sweep or complete ledger here.

## Mobile distribution decision

Policy checked November 16, 2026 against official sources below. Custom downloadable
music is not automatically a physical or person-to-person service exception. Apple
3.1.1/3.1.3 distinguishes digital access, real-time person-to-person services and goods
consumed outside the app; Google distinguishes digital goods/services from listed
exceptions and enrolled region-specific programs. Classify the actual delivered product,
where it is consumed, storefront/region and applicable program before release. A generic
browser button or external link does not settle eligibility. No compliance approval is
claimed here; the mobile lab contains no checkout-launch action.

## Acceptance matrix

| Case | Evidence status |
| --- | --- |
| Server amount/stable key/fixed URL/request redaction | Simulated HTTP adapter contract tests |
| Raw signature/altered bytes/skew/wrong secret/rotation header | Portable crypto tests + independent Python vector |
| Duplicate/concurrent/out-of-order/reopen/transaction rollback | Real temporary SQLite tests |
| Signed test event -> owned Paid receipt | Actual in-process JWT/API/verifier/store integration with simulated provider |
| Forged return/cancel, body price/card/paid fields, cross-owner read | API integration tests |
| Controlled Paid production gate / cancellation review | Portable fixture only |
| Hosted Stripe sandbox session/card/CLI webhook end to end | Pending configured provider execution |
| Native status refresh/session switch/TLS and iOS build | Pending device/platform execution |
| Refund/dispute/reconciliation sweep/audit/backup | Not implemented; later operations/integration work |
| Storefront/program eligibility and approval | Pending product/storefront decision and release review |

Primary references checked 2026-11-16:
- https://docs.stripe.com/api/checkout/sessions/create
- https://docs.stripe.com/api/idempotent_requests
- https://docs.stripe.com/webhooks
- https://docs.stripe.com/testing
- https://developer.apple.com/app-store/review/guidelines/#payments
- https://support.google.com/googleplay/android-developer/answer/9858738
