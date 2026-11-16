# Building Mobile Apps with C# 15

Current expanded source reaches Chapter 35: published loopback HTTPS staging,
explicit schema migration, verified online backup/new-directory restore, readiness
review and allowlisted correlated diagnostics. A local mobile consent lab and bounded
newsletter confirmation port keep their own consent boundaries. See
[the operations worksheet](docs/operations-lab.md) and [verification](docs/verification.md).
Public hosting, remote crash reporting, real provider/customer recovery and native/iOS
acceptance remain pending.

Companion code for **.NET 11 and .NET MAUI 11: A Windows Developer’s Guide to Android, iPhone, and Tablets**, by John F. Gnazzo.

## Start learning

Follow [the reader learning guide](docs/learning-guide.md) for setup, a source/test
map, expected results and troubleshooting. Begin with [the Part I foundation lab](docs/labs/part01-foundation.md)
for Chapters 1–5, or use [the guided Chapter 18 API lab](docs/labs/ch18-api.md)
to run the service and client, inspect paging and work through failure cases.

Current executed evidence and exact source revision are in
[verification](docs/verification.md): 636 portable tests and 107 API tests pass;
Android compiles and the published HTTPS staging/recovery drill passes. Earlier milestone notes below
are historical. Native/provider and iOS observations remain pending.

## Run portable checks on Windows

Install .NET SDK 11.0.100 or a later patch in its feature band. In PowerShell at the repository root:

```powershell
dotnet --info
dotnet build tests/GnazzosWorld.Core.Tests/GnazzosWorld.Core.Tests.csproj -c Release
dotnet run --project tests/GnazzosWorld.Core.Tests/GnazzosWorld.Core.Tests.csproj -c Release --no-build
```

The last command runs xUnit's generated executable runner, so no separate VSTest adapter is required. Its failure exit code fails CI. Do not substitute arbitrary dotnet test options intended for another test platform.

## Mobile scaffold

Use compatible Visual Studio 2026 and the MAUI workload on Windows. After installing the supported Android SDK/JDK versions:

```powershell
dotnet workload restore src/GnazzosWorld.App/GnazzosWorld.App.csproj
dotnet build src/GnazzosWorld.App/GnazzosWorld.App.csproj -f net11.0-android
```

The scaffold selects Android on Windows/Linux and adds iOS on macOS. A Windows-to-Mac remote iOS build configuration is not implemented yet. Do not read the conditional framework list as a claim that Windows can build/sign iOS independently.

The application ID and minimum OS versions are provisional development values. The local public-fixture API is available separately. No identity tenant, MFA, checkout, real media, or production deployment exists yet. Data is illustrative; the podcast episode is fictitious. Prices are a snapshot, not a live commercial offer. No credentials are needed for portable examples.

Manuscript: https://github.com/minnesotabob/csharp-mobile-app-man

## Rights

Copyright 2026 John F. Gnazzo. A distribution license for companion code has not yet been selected; public visibility alone does not grant a general license. Third-party dependencies retain their own licenses.

## Part III in progress: Chapter 12 catalog metadata

Catalog items now support stable creator references, optional release dates, book editions,
audio durations, availability, and explicit-content advisories. The bundled snapshot validates
creator references before it is returned. Unknown dates and advisories remain unknown.
The existing browser continues to use the same five records and three content kinds.
Metadata labels and artist detail pages are not wired into the native UI yet.

See `CatalogMetadataTests.cs` for import-boundary regression cases. The 61-test result
above records the earlier Part II milestone; Chapter 12 adds 22 cases: all 83 tests pass with a clean portable Release build. See the verification record for exact source revisions and the Android build.

## Part III in progress: Chapter 13 discovery

The browser now combines text, kind and creator filters, stable title/newest-release sorting,
and a favorites-only view. A collapsible filter panel preserves browsing space. Typed input
uses a 250 ms debounce; clearing is immediate and canceled/stale input cannot apply later.

Favorites are item IDs stored locally through MAUI Preferences. The teaching format is bounded
to 32 IDs, 128 characters per ID and 4096 serialized UTF-8 bytes. Save failures do not change
the visible set. An unreadable initial store disables favorite edits and offers reload while
ordinary browsing remains usable. There is no automatic destructive repair.

At the Chapter 13 source revision, all 112 portable tests pass and Android Debug compilation
succeeds. Actual native restart/persistence, accessibility, keyboard/layout behavior and iOS
builds remain pending. No remote search or cross-account synchronization is implemented.
See [verification](docs/verification.md) for exact revisions and CI evidence.

## Chapter 15 draft

The Home screen opens a three-step custom-song request builder with conditional
vocal notes, portable validation, review invalidation and explicit device-local
draft save/discard. Review sends nothing. Anonymous JSON drafts are not app-encrypted
or account-scoped. Its portable cases now run within the verified Chapter 18 suite;
native checks remain pending. See docs/verification.md. Recovered Chapter 14 changes are included in this source.

## Chapter 16 demo

The request review now offers archived USD pricing for one to three songs with
the same specification, a 15-minute demo quote, and simulated submission.
The simulator creates no live order or payment and uses only process memory.
An uncertain response preserves the same key/quote/request for explicit retry.
See SubmissionTests.cs and docs/verification.md for evidence and limits.

## Chapter 17 networking lab

Open Networking lab from Home to run a GET through an explicitly in-process
HTTP handler. No server is contacted; the main catalog remains bundled.
HttpCatalogSource bounds bodies at 1 MiB, uses one 20-second deadline, permits
at most three GET attempts and honors bounded Retry-After waits. It rejects
malformed payloads and exposes safe typed failures. Real API/local networking
integration follows in Chapter 18. See HttpCatalogTests.cs and docs/verification.md.

## Chapter 18 public fixture API

The ASP.NET Core companion now exposes versioned public catalog documents, the
earlier items array, revision-aware paging and development OpenAPI. Run the service
and HTTPS probe using docs/local-api.md. The default API has no customer order writes or deployment; Chapter 32 adds separately opt-in synthetic order routes.
Tests cover the actual pipeline; a separate Kestrel loopback HTTP smoke checks startup.
The native main catalog and networking lab retain their earlier bundled/demo modes.
