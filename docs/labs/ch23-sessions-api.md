# Chapter 23 — sessions, secure storage and owned API reads

## Executable boundaries

The API adds JWT bearer middleware and one read route, `/api/v1/requests/{id}`.
It validates issuer, audience, signature, algorithm and lifetime; its policy requires
`requests:read` and one nonempty subject. Record lookup filters by validated issuer
and subject. No customer records are seeded in the running API, and no write route
is introduced. Tests inject fictitious records and ephemeral RSA metadata through
test-host DI only. They do not replace the bearer handler with a fake login handler.

The separate native Session lab uses Auth0 SDK token acquisition/refresh, one MAUI
SecureStorage envelope, and a bounded owned-read client. Both configuration properties
still ship null. Chapter 22's process-only Account example stays separate and does not
silently migrate favorites, drafts or Chapter 21 replicas into an authenticated account.

## Run portable and API checks

```powershell
$core = "tests/GnazzosWorld.Core.Tests"
$api = "tests/GnazzosWorld.Api.Tests"
dotnet build $core -c Release
dotnet run --project $core -c Release --no-build
dotnet build $api -c Release
dotnet run --project $api -c Release --no-build
```

Follow `ApiSessionTests.cs` for durable-envelope contracts with an explicit memory
vault, renewal serialization, uncertain rotation, storage failure, sign-out fencing,
401 handling, 403/404/redirect handling and a bounded response. Those doubles do not
prove native encryption or actual Auth0 renewal.

Follow `RequestAuthorizationTests.cs` for signed test tokens through real JWT middleware:
valid owner, another owner, missing scope/subject, invalid signature, issuer, audience,
expiry, malformed input and default unconfigured rejection. The test private RSA key
is generated per factory, never shipped as API signing configuration. Existing public
catalog tests and the loopback HTTP smoke remain part of the verification workflow.

## Configure a development provider and API

Complete the Chapter 22 live-tenant setup first. In your provider's development API
registration, configure an HTTPS resource identifier, define `requests:read`, and use
a scoped delegated access token. Enable offline access and refresh-token settings only
if needed; review current provider behavior and service terms. Verify requested scope
is actually granted. Do not assume requesting a scope grants permission.

Set API environment values to your real registration, for example:

```powershell
$env:Identity__Authority = "https://YOUR-TENANT.us.auth0.com/"
$env:Identity__Audience = "https://YOUR-API-IDENTIFIER"
dotnet run --project src/GnazzosWorld.Api
```

These are public identifiers, not credentials. Use trusted HTTPS on the device-facing
API; do not disable certificate validation. The existing development listener and
public loopback smoke are not evidence of phone TLS reachability.

In `AuthenticationConfiguration.cs`, configure `ApiSession` using the Chapter 22
identity, the same API identifier and your trusted HTTPS API origin. Example shape:

```csharp
public static ApiSessionSettings? ApiSession { get; } =
    new(new("your-tenant.us.auth0.com", "YOUR_PUBLIC_CLIENT_ID"),
        "https://YOUR-API-IDENTIFIER", new Uri("https://YOUR-API-HOST/"));
```

Replace the placeholders. Do not change this origin based on a shared link, request ID
or server error. Do not add a client secret. The sample restricts resource identifiers
to HTTPS URIs for a clear teaching configuration.

Home > Open session lab can sign in for the API, read a test request and sign out
locally. The server's store is deliberately empty, so a valid scoped real login will
receive 404 until a controlled authenticated fixture/data implementation is supplied.
This is not a public route for creating arbitrary customer records. A live provider
round trip and real owned-record observation remain pending, not supplied by the tests.

## Session contract

- Access credentials are opaque on the client. A 60-second expiry margin triggers one
  serialized refresh. The SDK owns the exchange; the application does not parse a JWT.
- A returned rotated refresh credential and access credential are saved in one versioned
  envelope before the access credential is exposed to a caller.
- An ambiguous refresh failure clears the local credential envelope and requires
  interactive recovery. It does not automatically replay a potentially consumed refresh.
- Storage read/write failures block credential use; there is no Preferences/file fallback.
  Retry local sign-out to overwrite the credential with a signed-out marker.
- If that overwrite fails, current-process use is blocked but persistent removal is NOT
  established. A restart can encounter the old envelope; do not claim global sign-out.
- A first-install Preferences marker is written only after overwriting this app's secure
  session entry. This handles the possibility of retained iOS Keychain data. Android
  backup is disabled in the existing manifest. Native backup/reinstall behavior still
  needs platform tests; the Preferences marker is a lifecycle aid, not an identity proof.
- Leases include a generation. Local sign-out invalidates old leases before waiting for
  the worker; stale replies cannot repopulate the session or publish a preview.
- Bearer credentials are attached to one request, never HttpClient default headers.
  The native handler disables redirects. The client does not follow 3xx responses or
  blindly refresh on 403/404, and never replays a customer write.
- A 401 ends only the corresponding current lease and asks for sign-in. It does not
  assume expiry or start an automatic retry loop.

One ApiSession owns one vault. The code does not coordinate multiple processes or
independent coordinators sharing the same secure entry. This lab does not implement
provider browser logout, remote revocation, global session termination, deletion or
migration of existing per-user application data.

## Native/provider acceptance worksheet

Record source, packages, tenant/API references, device/OS, scenario and sanitized result.
Never retain access/refresh tokens, passwords or recovery codes in screenshots or logs.

1. Sign in with a disposable user. Verify issuer/resource setup and granted scope. The
   application must not treat its local identity display as API authorization.
2. Restart and read through the vault. Then approach access expiry and verify one SDK
   refresh, durable rotation and subsequent API acceptance against the real provider.
3. Sign out while renewal or an owned read is delayed. Confirm that an older result
   cannot restore identity or display the earlier account's preview.
4. Exercise unavailable/unreadable SecureStorage and a controlled write failure. Confirm
   actionable recovery with no plaintext fallback and no false claim of completed cleanup.
5. Reinstall/restore on Android and iOS independently. Record retained Keychain/backup
   observations instead of inferring them from a unit test.
6. Revoke the test user's grant using the provider's supported development controls.
   Verify that failed renewal leads to sign-in. Local clearing alone does not revoke it.
7. Capture native anonymous/session-lab, denied and successful owned-read states using
   approved fictitious data. All these observations remain pending until actually run.

## Sources

- https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication
- https://learn.microsoft.com/dotnet/maui/platform-integration/storage/secure-storage
- https://auth0.com/docs/secure/tokens/refresh-tokens/refresh-token-rotation
- https://auth0.com/docs/secure/tokens/refresh-tokens/revoke-refresh-tokens
- https://auth0.com/docs/get-started/apis/scopes/api-scopes
