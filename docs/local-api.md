# Chapter 18 — run the public fixture API

This local teaching backend serves public demo catalog data and an original public
WAV. Chapters 23–26 also add an independently authorized request-preview route;
that preview is not a private media-delivery service. Default configuration has no customer order writes. Chapter 32 separately enables
synthetic SQLite quote/order writes: see commerce-lab.md. There is no production
database, live media proxy, payment or deployment in this lab.
Chapter 30 separately adds protected push-registration PUT/DELETE operations; these
are bounded volatile registry writes, not an outbound notification sender.
Do not use real customer stories or contact data. The app's main catalog and its
Chapter 17 native lab remain bundled/in-process; they are not silently reconfigured.

## Windows workstation

Use the pinned SDK from global.json. From the repository root:

```powershell
dotnet dev-certs https --trust
dotnet run --project src/GnazzosWorld.Api --launch-profile https
```

Keep that terminal running. The launch profile uses Development and loopback
https://localhost:7443 plus http://localhost:5080. In a second terminal:

```powershell
Invoke-RestMethod https://localhost:7443/health
Invoke-RestMethod 'https://localhost:7443/api/v1/catalog/pages?limit=2'
dotnet run --project tools/GnazzosWorld.CatalogProbe -- https://localhost:7443/api/v1/catalog
```

The probe uses the actual bounded HTTPS client, validates version/revision and
creator relationships, and prints counts only. Ctrl+C cancels. It rejects HTTP,
embedded credentials and fragments. A valid workstation HTTPS check does not
establish that Android/iOS trust the same certificate.

Open https://localhost:7443/openapi/v1.json for the machine-readable development
contract; an interactive Swagger/Scalar UI is not installed. Production does not
map that document endpoint. New deployments and signing/account setup are separate.

## Routes and wire contract

| GET route | Response |
|---|---|
| /health | Minimal status object |
| /api/v1/media/timing (GET/HEAD) | Original WAV, strong ETag, bounded HTTP byte ranges |
| /api/v1/catalog | V1 document: schemaVersion, revision, items, creators |
| /api/v1/catalog/items | Chapter 17-compatible complete items array |
| /api/v1/catalog/pages | Offset/limit page with revision, total and nextOffset |

The complete fixture is limited to 1,000 items/creators by the document validator;
the shared client still bounds transfer bytes to 1 MiB. The shipped fixture has
five items. The unpaged routes are teaching contracts, not unlimited production
exports. V1 deliberately shares immutable catalog record shapes with the wire DTO.
Schema evolution must preserve that contract or introduce another version.

Paging defaults: offset=0, limit=20. Limit must be 1..100. Duplicated, unknown,
malformed, negative or overflowing numeric parameters return 400 validation problems.
For offset>0, supply the revision from the first page. A different revision returns
409; restart paging. offset==total returns an empty terminal page; greater offsets
are rejected. Order is ordinal by stable item ID. Revision changes must accompany
any changed fixture content. This is not a database cursor or cross-server snapshot.

Unexpected failures return a generic 500 problem without exception details, even in
Development. Public catalog writes return 405; customer request/order/quote routes
do not exist. API integration tests verify these boundaries.

## Environment and configuration

Catalog:Revision comes from appsettings.json, with a fixed fallback fixture revision.
Override with Catalog__Revision or a command-line configuration value when deliberately
changing the fixture. It must contain 1..80 ASCII letters/digits, dots, underscores
or hyphens. Invalid fixture/configuration fails startup. No secrets belong in this
setting or repository. ASP.NET Core's normal configuration precedence applies.

AllowedHosts is restricted to localhost and 127.0.0.1. That checks request hostnames;
it does not create a listener or act as authentication. Default listeners stay
loopback. Adding a reachable hostname, listener or tunnel is a deliberate reader
integration step, never something performed by these instructions automatically.

## Device networking map

| Where the client runs | Meaning of localhost | Development route |
|---|---|---|
| Windows probe/browser | Windows workstation | localhost:7443 with trusted matching certificate |
| Standard Android emulator | Emulator itself | Host alias 10.0.2.2; TLS name/trust still need a deliberate solution |
| iOS simulator on paired Mac | Mac running the simulator | Windows API needs a reachable Windows host or chosen HTTPS tunnel |
| Physical Android/iPhone | That device | Reachable workstation hostname with matching trusted TLS, or chosen HTTPS tunnel |

Changing localhost to 10.0.2.2 does not make the localhost development certificate
valid for that IP address. Trusting a certificate on Windows does not install trust
on a phone. Keep certificate verification and the client's HTTPS restriction enabled.
No permissive certificate callback, cleartext mobile exception or wildcard host rule
is installed by this milestone.

One optional public-fixture-only route is a temporary development tunnel, using
Microsoft's current dev-tunnels setup. Hosting requires the reader's account login.
A default tunnel requires access authorization; the probe deliberately refuses
redirects to a browser login page. If the reader explicitly chooses anonymous
access for these public fixtures, the documented pattern is:

```powershell
devtunnel user login
devtunnel host -p 5080 --allow-anonymous
```

This exposes the chosen local port through a public HTTPS address while hosting.
Do not run it for private/customer endpoints. Use the exact printed HTTPS hostname
plus /api/v1/catalog with the probe. If host filtering rejects the request, add only
the exact chosen hostname to AllowedHosts and restart; never assume the proxy's Host
behavior. Stop tunnel hosting when finished. Tunnel setup, native trust, network
policy and actual device connections remain unverified here.

Before a mobile integration, prove the chosen endpoint works from the device browser,
then use HttpCatalogDocumentSource with that endpoint and an app-lifetime HttpClient
whose handler refuses redirects and preserves TLS validation. Follow platform
network permissions and test the actual configured handler. The current native lab
still demonstrates the fixture handler; it does not contain a real-service switch.

## Repeatable verification

```powershell
dotnet build tests/GnazzosWorld.Core.Tests -c Release
dotnet run --project tests/GnazzosWorld.Core.Tests -c Release --no-build
dotnet build tests/GnazzosWorld.Api.Tests -c Release
dotnet run --project tests/GnazzosWorld.Api.Tests -c Release --no-build
dotnet build tools/GnazzosWorld.CatalogProbe -c Release
./eng/Smoke-Api.ps1
```

WebApplicationFactory tests exercise the real ASP.NET Core pipeline through TestServer;
they do not open a socket or validate certificates. Smoke-Api.ps1 starts actual Kestrel
on a temporary loopback HTTP port, checks health/catalog/paging, then stops the process.
That establishes host startup and plain local HTTP only. The HTTPS probe must be run
against the reader's chosen trusted endpoint to establish that TLS integration.

Sources:
- https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/openapi
- https://learn.microsoft.com/en-us/aspnet/core/testing/integration-testing
- https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api
- https://learn.microsoft.com/en-us/dotnet/maui/data-cloud/local-web-services
- https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/cli-commands

## Chapter 29 public range fixture

See download-lab.md for the descriptor, foreground downloader and pending native/TLS
checks. Results.File owns Range/If-Range/HEAD processing; no path parameter or private
delivery identifier is accepted. API tests and the loopback smoke check actual bytes.

## Chapter 30 optional push registry

PUT/DELETE /api/v1/notification-installations/{canonicalInstallationId} require a
validated credential with notifications:manage. Body <=4096 bytes, current owner
claims, monotonic generation, four installations/owner, 100 total and 24-hour volatile
expiry. See notification-link-lab.md. No GET token inventory or outbound send endpoint
is supplied; real provider credentials/delivery and durable production storage remain
pending. Protected read and push registration use separate coarse process budgets.

## Chapter 33 Development test payments

See payment-lab.md for separate Payments opt-in, secret/account/return-origin settings
and the four supported snapshot event types. Live keys and non-Development mapping are
rejected. Provider test checkout is a developer-browser exercise; the mobile app only
reads owned TEST status. No browser return writes payment state. Default API behavior
remains payment-disabled. Actual hosted Stripe/CLI execution remains pending.

## Chapter 34 Development tracking/delivery

See fulfillment-lab.md for separate Fulfillment opt-in, owned paging/history/revision
routes and authenticated short-lived-grant sample reads. Use the supplied local console
fixture with an explicitly disposable database to stage reviews/deliveries. It manufactures
synthetic TEST paid evidence and is not production administration. The fixed WAV is not
served through a static directory or public cache. Native memory verification is separate
from listening/offline storage, and notices are not provider push delivery receipts.
