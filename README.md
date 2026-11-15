# Building Mobile Apps with C# 15

Companion code for **.NET 11 and .NET MAUI 11: A Windows Developer’s Guide to Android, iPhone, and Tablets**, by John F. Gnazzo.

## Foundation milestone (not a finished app)

- Portable net11.0 catalog models, bundled sample data, search, and versioned example pricing.
- xUnit v3 executable tests. The major package line is v3; its package version is 4.0.0.
- MAUI 11.0.10 Android/iOS first-screen scaffold; mobile compilation/device verification status is in docs/verification.md.
- Windows CI for the portable tests. Mobile builds are not included in that workflow yet.

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
