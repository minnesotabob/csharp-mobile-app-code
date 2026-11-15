# Chapter 25 - profiles, privacy and account lifecycle

## Scope and run

A portable local SQLite lab with explicit simulated lifecycle receipts. No native settings
page, remote export/deletion endpoint, reauthentication adapter, Auth0 deletion, notification
registration or authenticated synchronization is enabled by this milestone. Chapter 23's
session lab remains separate. Do not connect a real account to this simulation.

From the repository root with the pinned toolchain:

```powershell
dotnet build tests/GnazzosWorld.Core.Tests/GnazzosWorld.Core.Tests.csproj -c Release
dotnet run --project tests/GnazzosWorld.Core.Tests/GnazzosWorld.Core.Tests.csproj -c Release --no-build
dotnet run --project samples/GnazzosWorld.AccountLab/GnazzosWorld.AccountLab.csproj -c Release
```

The console creates a unique temporary SQLite directory and removes it on normal completion.
It reports A sync eligible, empty B, rejected old A handle, pending-removal sync blocking,
stable retry ID, SIMULATED reauthentication/acceptance/completion, and unaffected B.
No real customer or provider account is modified. A terminated process may leave its
unique temporary directory; remove only that lab-created directory after inspection.

## Files and contracts

- src/GnazzosWorld.Core/Identity/AccountWorkspace.cs: owner-partitioned profiles, immutable
  preferences, explicit synchronization choice, revision checks, scoped export, durable
  operation IDs and transactional local purge/receipt application.
- tests/GnazzosWorld.Core.Tests/AccountWorkspaceTests.cs: account switching/reopen, consent,
  stale editor, wrong receipt, reorder, restart and injected SQLite-purge failure cases.
- samples/GnazzosWorld.AccountLab/Program.cs: deterministic scenario with simulated receipts.

An AccountOwner is a partition key, not identity proof. Production code must derive it from
validated authentication. A WorkspaceLease is an instance-local handle; detach, activation
or coordinator replacement invalidates it. It is never an API credential. UI publication
must still check the current handle on the owning UI dispatcher after asynchronous work.

This separate workspace database does not migrate Chapters 13-21 favorites, drafts,
replicas, downloads or public catalog data. It has a fixed initial lab schema, one
coordinator and no cross-process writer guarantee. SQLite is not app-encrypted here.
Use fictitious data. Native files, backups and key management need later verification.

The consent notice ID and timestamp record this draft product choice, not a legal basis
or complete audit history. CanSynchronize is a dispatch decision for new work, not a
synchronization engine or revocation of already-sent data. OrderNotifications is an app
choice, not OS notification permission or proof that device tokens were unregistered.

ExportLocalProfile returns only this local profile in schema 1, explicitly scoped as
chapter25-local-profile. It excludes provider subject/issuer and other accounts. It is not
a full account-data export and has no remote download URL. Treat exported names as personal
data; do not log them or share them automatically.

## Request lifecycle experiment

1. Activate fictitious A, save a profile, opt into sync, and export the local profile.
2. Activate B. Check that it starts empty and A's old handle cannot read/write/apply receipts.
3. Return to A with a new handle. Queue deletion before sending anything. This durable
   intent immediately freezes local profile edits/exports and new sync eligibility.
4. Simulate a lost response: send no receipt. Reopen the file and queue the same kind.
   The same operation ID returns. A second transport request must use this ID; do not
   label it server idempotency until a real backend also enforces owner, kind and payload.
5. Apply an explicit simulated ReauthenticationRequired response. No authentication is
   performed by this enum. A real service must enforce the required freshness/assurance
   and ownership before accepting a privileged request.
6. Apply simulated Accepted. Profile remains available for status/recovery; do not say
   account deleted. A delayed provider worker or server failure still needs reconciliation.
7. Apply simulated Completed. Profile and the account's export intent are removed in one
   SQLite transaction with the deletion receipt update. B's profile is unaffected.
8. Retry and deliver an older receipt. Completion remains monotonic; profile is not recreated.
9. Inspect the rollback test: a temporary SQLite trigger aborts purge. Accepted state and
   profile remain, then the same operation can complete after the failure is removed.

One operation per owner/kind is retained in this bounded lab. It does not implement later
repeat export requests, cancellation of deletion, service rejection recovery, scheduled
removal, multi-device coordination or account re-creation after completion. A future service
must define these transitions before expanding the model. An export operation queued before
deletion is not a grant to dispatch it during deletion; recheck the account removal status.

## Sign-out, deletion and retention promises

Detach invalidates this coordinator's handle; saved profiles remain partitioned for the
same account's return. It does not clear tokens. Coordinate with ApiSession.SignOutLocalAsync
in a future native composition and report storage failures honestly. Local sign-out does not
revoke grants, end provider SSO, cancel billing or delete server records.

Completion here is local row removal after a caller-supplied simulated service receipt.
The deletion receipt retains issuer/subject, operation ID and state to prevent resurrection.
It has no implemented expiry. SQLite page bytes/backups are not physically erased, and
remote stores, providers and third-party processors are untouched. This is not a complete
privacy-deletion implementation. Production needs disclosed retention purpose/duration,
protected audit access, backup expiry/restore rules and deletion verification for every store.

## Real integration acceptance record - all pending

Record source, tenant/API policy, device/OS, scenario, safe correlation reference and observed
result. Never include tokens, recovery codes, actual customer stories or raw export documents.

- Derive account ownership from the accepted session; reject a mismatched authenticated
  service receipt, another owner's request ID and stale assurance at the server.
- Reauthenticate for sensitive requests using the provider-supported hosted flow. An
  accepted client callback alone cannot authorize a protected server mutation.
- Retain the same operation ID after a timeout, poll status by authenticated owner, and
  observe pending, completed and actionable failure states without automatic duplicate jobs.
- Create a bounded export covering the advertised real stores; protect delivery/expiry and
  reauthorize retrieval. A local profile JSON file cannot certify this capability.
- Prevent writes and delayed sync resurrection while deletion is pending/completed; coordinate
  server account state and other devices, not just this in-process handle.
- Verify data-store removal, declared retained records, provider identity/grant handling,
  third-party cleanup, push-token removal and backup restoration behavior independently.
- Check anonymous browsing after sign-out and current-account data after switch, including
  queued requests, delayed callbacks, large text, keyboard and screen-reader focus.
- Provide discoverable deletion initiation in the app and the applicable external path,
  with accurate retention/delay/billing explanations. Review current store requirements.

## Sources

- https://developer.apple.com/support/offering-account-deletion-in-your-app/
- https://support.google.com/googleplay/android-developer/answer/13327111
- https://auth0.com/docs/secure/multi-factor-authentication/step-up-authentication
- https://learn.microsoft.com/dotnet/standard/data/sqlite/transactions
