# Chapter 26 - focused security verification

Read docs/security/threat-model.md first. Starting checkpoint is the Chapter 25 source
4cb8f9b9c85f38226ff562f9b01e7e6e676f9b64. Chapter 26 implementation begins at
a3d2a880195714dc61a2971bc2ab3f7aab1d78a9. Follow any corrective revision recorded in
verification.md. This lab uses controlled fixtures; it does not probe a live service.

## Run from the repository root

```powershell
dotnet build tests/GnazzosWorld.Core.Tests -c Release
dotnet run --project tests/GnazzosWorld.Core.Tests -c Release --no-build
dotnet build tests/GnazzosWorld.Api.Tests -c Release
dotnet run --project tests/GnazzosWorld.Api.Tests -c Release --no-build
```

Existing workflows also compile the API probe, run the real public loopback smoke,
run the two console labs and compile Android. Those checks remain distinct from native
TLS, browser return, hardware storage and provider observations.

## Trace one threat into enforcement

1. T01: read RequestAuthorizationTests. Keep real bearer middleware and ephemeral RSA
   fixture metadata. B's valid token cannot read A's record; a query label cannot change
   validated owner. Do not replace authentication with a fake handler for this claim.
2. T02/T03: inspect RequestIdPolicy and SecurityBoundaryTests. IDs cannot be URLs, encoded
   paths or account selectors. Lookalike publisher/provider hosts are rejected. SDK state
   validation and real OS app-link ownership remain independent requirements.
3. T05: inspect ProtectedReadSecurityTests. Eight authorized calls share one process budget;
   the next receives 429 and Retry-After. Switching subjects cannot reset it. The test does
   not sleep for a window reset or prove production throughput/fairness.
4. T06: inspect no-store for successful and unauthorized private responses, plus 400/429
   cases. Header verification does not prove all device/intermediary caches comply.
5. T07: capture only ReadSecurityAudit events from a fixture containing a token, query
   story, account label and malformed secret-looking ID. Check exact event 2601 outputs:
   granted, unavailable, invalid_identifier; none of those supplied values appears.
6. T04/T08: run the existing session/workspace suites. Old handles, late replies, foreign
   receipts and failed SQLite purge cannot silently republish a successful removal.

## Walk the complete protected-read pipeline

Program.cs registers AddProtectedReadSecurity, sets no-store for the private prefix,
authenticates/authorizes, applies UseRateLimiter and attaches the named policy to the
private route. RequestIdPolicy is shared with ProtectedRequestClient. No customer write,
file-download, lifecycle or admin endpoint is added. Public catalog remains anonymous.

An invalid token is rejected before rate limiting; a signed token missing scope yields
403. A scoped request consumes budget, then invalid ID shape yields 400, and owner lookup
yields 200 or generic 404. A depleted budget yields 429 before lookup. Authorization must
still be required if someone later refactors or replaces the limiter.

## Worked abuse case

B sends an owner-valid token and A's request ID, then adds ?account=reader-a. The API still
returns 404 because the lookup uses validated claims. After enough authorized reads B
gets 429, but that later response is not what established confidentiality. Rate limiting
reduces repeated downstream work; owner filtering prevents the first unauthorized read.

## Native/operator observations - pending

Complete the T-ID worksheet for real OS callback behavior, HTTPS failures, secure storage,
shared-device data clearing, provider MFA/recovery and remote lifecycle work. Review actual
framework/proxy logs separately from the custom event projection. Inventory customer files
and backup/retention before adding delivery. Follow documented package-advisory commands
with a real feed and record unknown/failure honestly; the unit tests do not scan packages.

The shared eight/minute budget is a bounded teaching setting. No auth-cost limiter, public
route cap, per-user fairness, multi-instance coordination or volumetric protection is
implemented. Event 2601 is a minimal projection, not a full sensitive-mutation audit trail.
