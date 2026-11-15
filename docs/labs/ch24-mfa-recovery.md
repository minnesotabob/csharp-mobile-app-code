# Chapter 24 - provider-supported MFA and recovery

This is a real-provider configuration and observation lab, not a simulated MFA service.
Application implementation remains Chapter 23 source 50a5abf391e37fef04866c0528af4da195954354.
No native or API behavior changes in this documentation milestone. Tenant configuration
ships null. Live enrollment, recovery, replacement and device checks are PENDING.

## Prerequisites and exact integration points

Complete ch22-identity.md and ch23-sessions-api.md first. Use a disposable tenant,
one disposable customer and the pinned toolchain in global.json.

- src/GnazzosWorld.App/Identity/AuthenticationConfiguration.cs: public development settings.
- src/GnazzosWorld.App/Identity/Auth0SessionProvider.cs: SDK interactive login and renewal.
- src/GnazzosWorld.Core/Identity/ApiSession.cs: persistence and session generation ownership.
- tests/GnazzosWorld.Core.Tests/ApiSessionTests.cs: portable session contracts.
- tests/GnazzosWorld.Api.Tests/RequestAuthorizationTests.cs: signed-token API gates.

## Bounded experiment

1. Record current tenant/application/connection and factor policy; keep admin recovery separate.
2. In development Security > Multi-factor Auth, enable an available independent factor
   and enable Recovery Codes separately. Record plan limitations instead of assuming access.
3. For isolated interactive tests select Always, save, and record remembered-browser settings.
   This is not a production recommendation. Refresh/noninteractive behavior needs its own run.
4. Configure the existing Chapter 23 Session lab using public identifiers and the exact
   callback. No native client secret, OTP seed or Management API token.
5. With a previously unenrolled disposable customer, enroll through hosted login.
   Save recovery material in a secure place independent of the test phone; never in this lab.
6. Observe native return and independent API result. The default API has no customer rows;
   a 404 is not proof that a private request preview was restored.
7. Start a fresh hosted transaction. Check bounded incorrect response, cancellation, retry,
   app background/return and accessible controls. Local clearing does not end browser SSO.
8. Use hosted recovery without the usual authenticator. Securely retain the replacement
   credential. In a fresh transaction test rejection of the consumed credential.
9. For disposable customer replacement, use the documented development administrator reset
   and repeat hosted enrollment. Record issuer/subject continuity. Recovery alone does not
   establish factor removal. No self-service factor-management UI ships in this sample.
10. Observe SDK renewal under the actual policy. If interaction is required, the existing
    sample asks for deliberate sign-in; do not relax policy or loop background challenges.
11. Separately observe old factor, refresh grant, access token and browser-session behavior.
    Recovery and local sign-out do not imply remote revocation or instant API cutoff.
12. Preserve the previous policy record. Coordinate cleanup of disposable customer/tenant
    resources without changing shared or production settings.

## Evidence worksheet - fill only after observing

| Field | Value |
|---|---|
| Source / SDK / packages | Pending |
| Tenant policy revision / enabled factors / plan limits | Pending |
| Account alias (no credentials) / device / OS / browser | Pending |
| Enrollment and completed native return | Pending |
| Later challenge / remembered-browser conditions | Pending |
| Incorrect response / canceled challenge / deliberate retry | Pending |
| Recovery / replacement-code retention confirmed (no code) | Pending |
| Consumed credential rejected in fresh transaction | Pending |
| Factor reset/replacement / same stable subject | Pending |
| Renewal result / provider interaction requirement | Pending |
| Old factor / grant / token / browser-session outcomes | Pending |
| Independent API scope and owned-record result | Pending |
| Large text / screen reader / Android and iOS | Pending |
| Sanitized screenshot or correlation reference | Pending |

Do not store raw tokens, passwords, factor responses, provisioning QR codes or recovery
credentials. Record status as observed, expected, failed or pending. Stop on unexplained
bypass or changed subject; do not delete/recreate the account as a recovery shortcut.

## Worked check

A hosted recovery succeeds but the request read returns 404. This establishes neither
request restoration nor failed MFA by itself. Verify the accepted current session and API
scope/owner decision; the teaching store has no customer records by default. A late result
from a previous account must not become the recovered account's preview.

## Future step-up contract

No sensitive write or step-up API is implemented here. Before adding one, define owner,
permission, trusted provider assurance/freshness evidence and replay policy. An ID token,
client boolean, requested acr_values or recent token issue time alone is insufficient.
One approval per mutation requires an atomic operation-bound server record if that is
part of the product promise. Existing read-route tests do not certify that feature.

## Official references (reviewed November 15, 2026)

- https://auth0.com/docs/secure/multi-factor-authentication/enable-mfa
- https://auth0.com/docs/secure/multi-factor-authentication/configure-recovery-codes-for-mfa
- https://auth0.com/docs/secure/multi-factor-authentication/reset-user-mfa
- https://auth0.com/docs/secure/multi-factor-authentication/webauthn-as-mfa
- https://auth0.com/docs/secure/multi-factor-authentication/step-up-authentication
