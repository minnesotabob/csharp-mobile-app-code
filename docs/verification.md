# Foundation verification

Date: November 14, 2026 (Central time).

## Observed locally

.NET SDK 11.0.100, MSBuild 18.12.2, and .NET runtime 11.0.0 installed and answered dotnet --info on Debian 13. Local C# compilation/test execution was blocked during package restore; see the independent Windows CI result below.

Automatic approval review rejected both the local MAUI workload installation and the test/restore process because they attempted to contact the host cloud metadata endpoint, a potential credential boundary. They were not retried or bypassed. Static JSON/XML validation is independent of compilation.

## Safer verification route

The repository workflow uses a GitHub-hosted Windows runner, read-only repository permissions, and no repository secrets. It restores/builds only portable code/tests, then runs the xUnit executable. This avoids using the blocked local host for package restore/testing. Windows workflow run 43669410320 completed successfully at commit 0f05bf9c16a1e97118a2f85a56fc477cbe01ddd8. Its restore, Release build, and xUnit executable test steps all succeeded.

Evidence: https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43669410320

Static validation also passed for nine XML/XAML/project/plist/SVG files and five unique catalog records. This check does not compile or execute MAUI.

## Unverified

- MAUI Android and iOS compilation and launch.
- Android SDK/JDK and Xcode compatibility; chosen minimum OS targets are provisional.
- Emulator/physical device behavior, accessibility, signing, and release packages.
- MFA, backend, playback, payments, and deployment are future features.

SDK is pinned to 11.0.100 (latestPatch in that feature band); MAUI Controls is provisionally pinned to 11.0.10; xunit.v3 package is pinned to 4.0.0. Use no preview language switch.
