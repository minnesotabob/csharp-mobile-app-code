# Building Mobile Apps with C# 15

Companion code for **.NET 11 and .NET MAUI 11: A Windows Developer’s Guide to Android, iPhone, and Tablets**, by John F. Gnazzo.

## Parts I and II milestone (not a finished app)

- Portable net11.0 catalog models, bundled sample data, search, and versioned example pricing.
- xUnit v3 executable tests. The major package line is v3; its package version is 4.0.0.
- MAUI 11.0.10 Shell navigation, typed bindings, MVVM browser, search/kind filtering, stable-ID details, reusable cards, English/Spanish resources, themes and adaptive inline details.
- Windows CI: 61 portable tests and Android Debug compilation passed. Device execution and iOS compilation remain pending; see [verification](docs/verification.md).
- See [source milestones](docs/milestones.md) to match the Part I starter or current Part II implementation.

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

The application ID and minimum OS versions are provisional development values. No identity tenant, MFA, API, checkout, real media, or production deployment exists yet. Data is illustrative; the podcast episode is fictitious. Prices are a snapshot, not a live commercial offer. No credentials are needed for portable examples.

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
