# Chapter 39 iOS release lab

This is an unexecuted native procedure. Begin at companion source
698aa3d431d0f7e39239c3a31a174263acf0295c, or a later documentation-only
revision whose production source is unchanged. Read the review worksheet before
running native commands. No iOS candidate, signing authority or store access is
provided by this lab.

## Environment and source

Use a dedicated checkout. Record `git rev-parse HEAD`, `git status --short`,
`dotnet --version`, `dotnet --info`, `dotnet workload --info`,
`xcode-select -p`, `xcodebuild -version`,
`xcrun --sdk iphoneos --show-sdk-version`, `sw_vers -productVersion` and `uname -m`.
Keep private filesystem/user details out of public reports.

The current platform release describes SDK 11.0.100, workload set 11.0.100.1,
iOS pack 27.2.11039 and Xcode 27.2. Apple's Xcode table lists macOS Tahoe
26.6+; the Microsoft release note says 16.6+, an inconsistent host requirement.
Use Apple's table for host planning and retain the discrepancy. This candidate
combination has not been installed or compiled for this application.

On a dedicated compatible Mac, after reviewing the release instructions:

```bash
dotnet workload install maui-ios --version 11.0.100.1
```

This changes the host. Record resulting versions rather than silently modifying
an established Android workstation. global.json pins an SDK feature band, not
all workloads or Xcode.

## Simulator first

Apple-silicon Mac procedure, not an execution record:

```bash
dotnet build src/GnazzosWorld.App/GnazzosWorld.App.csproj \
  -c Debug -f net11.0-ios \
  -p:TargetFrameworks=net11.0-ios \
  -p:RuntimeIdentifier=iossimulator-arm64 \
  -p:EnableCodeSigning=false
```

Use the appropriate simulator architecture on another supported host. The
explicit TargetFrameworks overrides this project's macOS-conditional iOS entry.
`ios-arm64` is the physical-device runtime, not the Arm simulator runtime.
Follow the current Microsoft launch procedure for a selected installed simulator.
Record compilation, actual launch, callbacks and scene lifecycle separately.
A build error must remain failed/pending; removing a dependency changes the candidate.

## Device identity and archive

Use the publisher's controlled team, explicit bundle ID, certificate/private key
and App Store provisioning profile. Development and ad-hoc device channels use
their own appropriate profiles/device registration. A certificate without its
private key cannot establish signing authority.

Set these public selectors to real installed values, then run on the Mac:

```bash
signing_identity='YOUR INSTALLED DISTRIBUTION IDENTITY'
provisioning_profile='YOUR APP STORE PROFILE'
dotnet publish src/GnazzosWorld.App/GnazzosWorld.App.csproj \
  -c Release -f net11.0-ios \
  -p:TargetFrameworks=net11.0-ios \
  -p:RuntimeIdentifier=ios-arm64 \
  -p:ArchiveOnBuild=true \
  "-p:CodesignKey=$signing_identity" \
  "-p:CodesignProvision=$provisioning_profile"
```

Inspect actual build output for fresh archive/IPA paths. Keep archives and matching
symbols controlled. Inspect the packaged Info.plist, signature, embedded profile,
entitlements, dependency manifests and frameworks. Record SHA-256, bundle ID,
version/build and source/tools. A hash compares bytes; it is not an attestation.

```bash
app_bundle='/actual/archive/path/Products/Applications/App.app'
plutil -p "$app_bundle/Info.plist"
codesign --verify --strict --verbose=2 "$app_bundle"
codesign -d --entitlements :- "$app_bundle"
```

## Windows remote publication

First establish a successful Mac-native path. Set up Visual Studio's supported
Pair to Mac workflow and saved SSH keys; do not place a login password in source
or a command. A remote command uses the same project, Release target, explicit
TargetFrameworks, ios-arm64, ArchiveOnBuild and signer/profile inputs as above,
plus these MSBuild properties:

| Property | Reader-supplied value |
| --- | --- |
| ServerAddress | Verified Mac host address |
| ServerUser | Controlled Mac system username |
| TcpPort | Documented build service port 58181 |
| _DotNetRootRemoteDirectory | Actual remote SDK directory |

The currently documented Visual Studio 2026 cache is under
`/Users/<user>/Library/Caches/maui/PairToMac/SDKs/dotnet/`.
Confirm it exists for the installed tooling. Omit ServerPassword only after
establishing the supported saved-key route. Keep remote publication pending until
it actually runs; a Mac-local archive does not pass this row.

## TestFlight and recovery

1. Prepare an App Store Connect record for the intended identity, truthful metadata,
   privacy/export answers and genuine phone/tablet captures.
2. Upload the actual candidate by a supported Apple route and inspect processing.
3. Assign internal/external groups as appropriate; observe beta-review requirements
   and the build's expiration. Apple's current overview describes a 90-day window.
4. Install build 1, save invented draft content and confirm success. End/reopen
   the process and verify recovery.
5. Install build 2 through the intended distribution path without uninstalling.
   Check the same confirmed draft in a fresh process; record both build numbers.
6. Test callback cancellation, permission denial, audio interruption, account
   switching, iPhone keyboard/VoiceOver and iPad layout/large text.
7. Reproduce reviewer access with invented records and provider-supported MFA/demo
   arrangements. No personal-phone dependency or hidden bypass.
8. Assess live account deletion, actual payments, privacy and production recovery
   before selecting a public candidate. Existing fixture labs do not pass these.

Do not revoke shared team certificates or uninstall a user's ordinary app as
cleanup. Clean only owned exercise resources. Record a failed update without
deleting its evidence or replacing it with a fresh-install observation.

## Primary references checked November 16, 2026

- https://github.com/dotnet/macios/releases/tag/dotnet-11.0.1xx-xcode27.2.11039
- https://developer.apple.com/xcode/system-requirements
- https://developer.apple.com/news/upcoming-requirements/
- https://learn.microsoft.com/dotnet/maui/ios/pair-to-mac
- https://learn.microsoft.com/dotnet/maui/ios/deployment/publish-cli
- https://developer.apple.com/help/app-store-connect/manage-builds/upload-builds/
- https://developer.apple.com/help/app-store-connect/test-a-beta-version/testflight-overview/
- https://developer.apple.com/app-store/review/guidelines/
- https://developer.apple.com/support/offering-account-deletion-in-your-app/
