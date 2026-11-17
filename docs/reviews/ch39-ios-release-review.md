# Chapter 39 source review and release worksheet

Reviewed November 16, 2026 (Central). Starting production source:
698aa3d431d0f7e39239c3a31a174263acf0295c. Documentation-only continuation;
no application implementation or workload configuration changes.

## Actual source findings

- global.json requests SDK 11.0.100, latestPatch, stable.
- App csproj defaults to Android; macOS adds net11.0-ios. Remote Windows commands
  need explicit evaluation of the iOS target, as described in the lab.
- Bundle ID com.johngnazzo.gnazzosworld; display 0.1.0/build 1; source iOS minimum 16.0.
- Info.plist declares iPhone/iPad, en/es, identity/content URL schemes, audio
  background mode and synthetic-lab photo-purpose strings.
- AppDelegate wires local notifications and gnazzosworld content links, delegating
  other URLs to the base path. Provider callbacks and scene lifecycle are unobserved.
- No checked-in Entitlements.plist or PrivacyInfo.xcprivacy under Platforms/iOS.
  This does not establish generated/dependency manifest presence or absence.
- Auth0.OidcClient.MAUI 1.4.0, MediaElement 12.0.0 and Controls 11.0.10 remain
  unchanged. Their actual iOS compilation/linking/initialization needs verification.

## Toolchain source finding

Microsoft's 27.2.11039 release specifies SDK 11.0.100/workload set 11.0.100.1 and
Xcode 27.2. Its macOS 16.6+ wording conflicts with Apple's Xcode table, which lists
macOS Tahoe 26.6+. Host planning uses Apple's table. Neither is a run record.

## Candidate worksheet

Fill source SHA, dirty changes, SDK/workload/pack versions, selected Xcode/build,
macOS/architecture, bundle ID, display/build versions, signer public metadata,
profile/channel/expiry, package hash, archive/symbol association, tester group,
device/OS, observation date and evidence location. Keep private keys, profiles with
private device lists, credentials, raw diagnostics and customer data controlled.

| Gate | Current result | Evidence required |
| --- | --- | --- |
| Source/configuration inventory | inspected | Findings above; not native evaluation |
| Simulator compilation | not_run | Actual command, source/tools, result |
| Simulator launch and callbacks | not_run | Named runtime and observed journeys |
| Signed device archive | not_run | Fresh archive, signature/profile inspection |
| Windows remote publication | not_run | Actual paired build result |
| Store processing | not_run | Candidate/build processing result |
| Physical iPhone/iPad journeys | not_run | Named devices and outcomes |
| Data-preserving distributed update | not_run | Builds 1/2, no uninstall, saved draft |
| Live privacy/account/payment acceptance | not_run | Actual provider/product evidence |
| Public release selection | unassessed | All required gates and publisher decision |

The planned Chapter 39 signed-candidate outcome remains incomplete. Historical
Android, API and portable evidence retains its original source and scope.
