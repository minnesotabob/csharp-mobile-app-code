# Chapter 22 — Native identity setup and verification

The sample adds Auth0.OidcClient.MAUI **1.4.0** to the MAUI app and uses its underlying
Duende.IdentityModel.OidcClient **6.0.1** engine in portable rejection tests. Auth0's
quickstart describes earlier .NET targets; this repository separately records actual
.NET 11 compilation. NuGet's computed compatibility does not establish device support.

## Unconfigured app

`AuthenticationConfiguration.Current` is null in distributed source. Home > Account
and sign-in explains the missing configuration; Home/Discover remain anonymous. No
provider request should be made. There is no embedded tenant, credential or live user.

## Configure your own development tenant

Use an Auth0 development tenant that you control; check the provider's current terms,
MFA availability and any cost before enabling optional services. This exercise does not
require a credit card, production provisioning or a chosen paid plan in the code.

1. In Auth0 Dashboard, open Applications > Applications and create a **Native** application.
2. Set Allowed Callback URLs to exactly `com.johngnazzo.gnazzosworld://callback/` and save.
3. Copy its Domain and Client ID. Never copy a client secret into the mobile application.
4. Set `AuthenticationConfiguration.Current` to `new("your-tenant.us.auth0.com", "YOUR_PUBLIC_CLIENT_ID")`.
   Use the actual lowercase tenant hostname, without `https://`, port or path. The
   sample deliberately permits standard `.auth0.com` domains only. Custom domains need
   an explicit reviewed configuration extension, not a redirect URI from user input.
5. Confirm a development login connection is enabled for that application. Use a
   disposable test user. Keep provider passwords, OTPs and recovery codes out of chat,
   source, diagnostic logs and chapter screenshots.

The callback's trailing slash is intentional. Configuration, SDK and native registration
must agree. The reverse-domain custom scheme is a teaching setup, not a claim of
exclusive ownership; evaluate claimed HTTPS links for a production design.

## Code map and platform configuration

- `src/GnazzosWorld.Core/Identity/NativeIdentitySettings.cs`: public configuration and
  allowed browser destination. This guard does not validate OAuth state or ID tokens.
- `SignInSession.cs` beside it: single attempt, cancellation, late-result fencing,
  sanitized messages, and process-only identity.
- `src/GnazzosWorld.App/Identity/Auth0InteractiveSignIn.cs`: SDK adapter; scope `openid profile`,
  no offline_access, no token persistence, no access token returned to the view model.
- `ProviderBrowser.cs` beside it: launches MAUI WebAuthenticator with the cancellation
  token. The pinned SDK's default browser does not forward that token; the explicit
  adapter makes the requested platform cancellation observable.
- Android callback activity: exact scheme/host/path, exported for the OS return; the
  manifest declares Custom Tabs visibility. No password WebView is added.
- iOS Info.plist: callback scheme registration. iOS compilation, signing and real callback
  handling require their own Mac/device verification.
- Home > Account routes to `AccountPage`; DI supplies one `SignInSession` and provider adapter.

Build using the existing Windows Android workflow or your verified MAUI workstation.
Run portable checks from the root:

```powershell
$tests = "tests/GnazzosWorld.Core.Tests"
dotnet build $tests -c Release
dotnet run --project $tests -c Release --no-build
```

## What to verify on an actual device

Record commit, SDK/package versions, tenant type, device/emulator, OS, UTC time and result.
Do not include credentials or returned tokens in the record.

| Scenario | Required observation | Current status |
|---|---|---|
| No tenant configured | Helpful configuration message; catalog still usable | Pending native run |
| Successful hosted login | Browser returns; display name changes; no customer API unlocked | Pending tenant/device |
| Browser Back/Cancel | No identity published; user can retry | Pending tenant/device |
| Cancel pending attempt | Underlying browser/callback closes or cancels; late success ignored | Pending tenant/device |
| Leave Account page | Pending operation canceled; later callback cannot create a local identity | Pending tenant/device |
| Mismatched registered redirect | Provider rejects; app never reports success | Pending tenant/device |
| Wrong-state/unsolicited callback | SDK rejects transaction; no identity published | Portable engine case only; native pending |
| Forget local sign-in | Local identity clears; provider browser cookie may remain | Pending native run |
| Restart app | Process-only identity is absent; anonymous catalog opens | Pending native run |

Capture Account's anonymous and signed-in states with fictitious profile data only.
Capture provider UI only with appropriate permission and all private fields excluded.
No screenshots or completed provider round trip are supplied by a passing compilation.

## Deliberate boundaries

The SDK generates/validates protocol correlation and PKCE. The app does not parse a JWT,
exchange codes itself, store passwords or accept arbitrary callback URIs as proof of
identity. Portable engine tests use static metadata and no-network handlers to exercise
invalid state, missing code and browser cancellation; no valid provider token is minted.
Other portable tests use explicitly simulated provider replies to check presentation.

Reader identity uses issuer plus subject, never email as a permanent account key.
"Forget local sign-in" is not provider logout, token revocation, account deletion or
clearing all application data. Existing local favorites/drafts and Chapter 21 replicas
are not attached to this identity. Chapter 23 handles sessions and API authorization;
Chapter 24 verifies provider MFA/recovery. No MFA assurance is inferred here.

## References inspected for this implementation

- https://auth0.com/docs/quickstart/native/maui
- https://www.nuget.org/packages/Auth0.OidcClient.MAUI/1.4.0
- https://github.com/auth0/auth0-oidc-client-net/tree/maui-1.4.0
- https://learn.microsoft.com/dotnet/maui/platform-integration/communication/authentication
- https://www.rfc-editor.org/rfc/rfc8252
- https://www.rfc-editor.org/rfc/rfc7636
