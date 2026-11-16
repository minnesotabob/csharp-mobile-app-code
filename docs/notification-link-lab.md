# Chapter 30 — notifications, deep links and sharing

Home → Notifications, links and sharing lab. Local notifications and public link sharing
are native adapter code, not verified device outcomes. Live FCM/APNs SDK acquisition,
provider credentials, outbound delivery, verified HTTPS domain association and iOS
compilation are pending. No provider project/key is bundled or silently activated.

## Local notification and route contract

- Permission is requested only by the explicit Choose button. Post checks permission
  and OS/channel availability again without forcing a prompt. Denial preserves public
  browsing. A successful post/schedule call is not delivery or presentation evidence.
- Android uses POST_NOTIFICATIONS where required, one named channel, an immutable
  PendingIntent and one replaceable lab notification ID. iOS requests alert permission,
  retains its notification-center delegate and schedules one replaceable local request.
- Foreground arrival presents a notification but does not navigate. Only its default tap
  action queues its bounded payload. Android handles OnCreate and OnNewIntent; iOS
  handles response callbacks. Existing identity callback schemes still go to their SDK.
- Canonical teaching link: gnazzosworld://open/catalog/song-minnesota-bob-northern-lights.
  Private pointer example: gnazzosworld://open/request/anniversary-demo. Exact scheme,
  host, destination, path and bounded ASCII ID only; no query, fragment, encoding,
  credentials, alternate origin or arbitrary Shell route is accepted.
- These custom-scheme links require an installed app, can collide with another handler
  and have no web fallback or verified domain ownership. Production HTTPS app/universal
  links require a real controlled domain and both platform association files/config.
- Notification envelope: schema 1, canonical message ID, canonical link, expiry within
  ten minutes. JSON <=1024 characters, no unknown or duplicate members/customer fields.
  Treat it as an untrusted hint, never proof of identity or permission.
- SQLite stores bounded untrusted IDs/links, no customer content or tokens. Up to eight
  pending hints and 128 retained rows; expired rows are pruned. Pending same-target hints
  coalesce. Completion retains a duplicate receipt until expiry. Navigation failure
  leaves the hint pending; the lab can retry or clear it. Queue expiry is not API access.
- Callback bridge marshals to the main thread. The single-window router waits for a
  loaded/active window and a Shell, then uses a fixed route name with typed ID parameters.
  Readiness/lifecycle ordering and cold OS callbacks require actual device validation.
- A request link opens a separate page. It fetches nothing automatically. Sign in then
  continue, or read with the current API session; ProtectedRequestClient and the server
  independently enforce credential, scope and owner lookup. Leaving/sign-out cancels the
  page operation. Local sign-out on this page also clears pending hints. No account from
  a payload is selected, and a link is never an authorization grant.
- Share opens the native sheet with one public catalog lab link. Returning from the
  sheet proves neither recipient selection nor transmission. Private request links,
  delivery URLs and tokens have no share builder in this lab.

The inbox is unencrypted installation-local routing metadata with a short TTL. Use
synthetic private IDs; backup, shared-device and privacy policy still need platform
review. Clearing receipts permits a previously handled payload to be accepted again
while valid; no global exactly-once behavior is claimed. Direct user links may be
opened again after completion; notification IDs deduplicate only within retention.

## Bounded push registration integration

The optional path is PushRegistrationCoordinator → HttpPushRegistrationSink → protected
PUT/DELETE /api/v1/notification-installations/{installationId}. This is registration
plumbing, not a live push receiver/sender. Native token SDK/provider acquisition is not
wired into the default application and no token is acquired/submitted by the lab page.

- Dedicated notifications:manage scope, validated token identity, canonical installation
  ID, bounded provider/token shape and monotonic generation. Owner comes only from the
  validated issuer/subject. The client uses the configured fixed HTTPS API origin,
  current session lease, no redirects and no response-body/token echo.
- The coordinator serializes rotation/removal. Its caller must supply persisted initial
  generation and persist the next generation BEFORE sending. Permission unknown/denied
  or missing token requests removal. A failed operation leaves confirmation false.
- Acquire/refresh real SDK tokens only after consent; observe token changes, recheck
  permission on resume and disable/unregister while the old account credential still
  exists. Stop/await the old coordinator before switching accounts. These native SDK,
  account-composition and durable cleanup/retry hooks remain unimplemented.
- The API has a 4096-byte body bound, separate eight-operation/minute process budget,
  four installations/owner, 100 total and 24-hour expiry. Registry is volatile, single
  process and has no production storage, provider token-validity proof or delivery job.
  Same installation cannot be rebound by another owner; a token cannot be bound to a
  second installation until removed/expired. Delete for a foreign owner has no effect.
- A conflict/timeout is not confirmation. Lost reply retries need a newly persisted
  generation. Revocation cleanup must not be reported successful when its request
  failed; production needs a durable outbox and bounded retry/expiry policy.
- FCM/APNs credentials belong to provider/server configuration, not the native bundle.
  Generic alert text and an opaque validated hint keep customer text off the lock screen.
  Fetch authorized state after an explicit user action; push is not a source of truth.

## Native acceptance (all pending)

Record exact SHA, model/OS, permission/channel state, launch state, steps and evidence.

| Scenario | Required observation | Android | iPhone |
| --- | --- | --- | --- |
| Permission granted/denied/revoked | Explicit prompt only; later post rechecks; no forced re-prompt | Pending | Pending |
| Local notification in foreground | Presentation follows OS settings; arrival never navigates | Pending | Pending |
| Tap while warm/cold/backgrounded | Valid hint persists; navigation waits for ready UI | Pending | Pending |
| Duplicate/expired/malformed hint | No repeated notification-ID navigation or arbitrary route | Pending | Pending |
| Request tap while signed out | Explicit sign-in continuation, then independent owned read | Pending | Pending |
| Cancel/sign-out/leave during read | No late private result published into obsolete page/session | Pending | Pending |
| Public share sheet | Public link only; cancel/return is not transmission proof | Pending | Pending |
| Custom-scheme collision/no installed app | Document absence of verified association/web fallback | Pending | Pending |
| Live SDK token rotation/permission loss/sign-out | Provider acquisition and account cleanup still require composition | Pending | Pending |
| Real FCM/APNs delivery | Provider project, credentials, APNs entitlement/device path required | Pending | Pending |

Portable tests use real SQLite plus simulated registration sink/clock. API integration
uses signed synthetic credentials and real middleware, including actual session sink
registration/removal. Tests do not prompt, display, tap, share or deliver a push. See
verification.md for exact executed source and CI results.
