# Chapter 21 — Synchronization and conflict resolution

## What runs

A portable .NET 11 console lab and xUnit cases. Two real-file SQLite replicas talk to
one in-memory `SimulatedSyncServer` through an account-bound fixture transport.
There is no HTTP sync route, authentication, production server, MAUI wiring, secret
storage, encryption, or OS job. The existing Chapter 18 API remains public-catalog only.
Use fictitious text. Each replica file is bound to one simulated account; selecting
that account string is a test setup operation, not proof of identity.

Run from the repository root with the SDK in `global.json`:

```powershell
dotnet build tests/GnazzosWorld.Core.Tests/GnazzosWorld.Core.Tests.csproj -c Release
dotnet run --project tests/GnazzosWorld.Core.Tests/GnazzosWorld.Core.Tests.csproj -c Release --no-build
dotnet run --project samples/GnazzosWorld.SyncLab/GnazzosWorld.SyncLab.csproj -c Release
```

Expected console transcript:

```text
Conflict: True; local=Tablet chorus; remote=Phone verse
Resolved draft on phone: Tablet chorus
Stale favorite conflicts with deletion: True
Favorite remains deleted: True
Local protocol fixture only; native and authenticated synchronization remain pending.
```

The lab creates disposable files in a unique temporary directory and removes them at
exit. It does not reuse the app's catalog database or migrate existing favorites.

## Follow the source

- `src/GnazzosWorld.Core/Sync/SyncProtocol.cs`: keys, versions, tombstones, operation
  identity, bounded value rules and transport contract.
- `SimulatedSyncServer.cs` beside it: per-context rows, immutable change history,
  duplicate receipts and optimistic version comparison, under one process lock.
- `UserSyncReplica.cs` beside it: durable outbox, local projection, explicit conflict
  resolution, one worker per owning replica, transactional page and cursor application.
- `tests/GnazzosWorld.Core.Tests/UserSyncTests.cs`: lost reply/reopen, cancellation
  after acceptance, two-device conflicts, deletion, account separation, pagination,
  rollback, malformed pages and concurrent worker calls.

## Experiment

1. Run the console lab unchanged. Identify the two conflicting texts and the user's
   explicit resolution. A conflict does not silently discard the local text.
2. Change `keepLocal: true` to `false` for the draft; the phone retains `Phone verse`.
   Restore the lab afterward. Both policies are deliberate choices, not automatic merges.
3. Inspect the lost-acknowledgment test. A server acceptance happens before an injected
   `IOException`. Reopening the replica retains the original operation ID. A replay
   returns its receipt rather than producing another change event.
4. Inspect the trigger-injected cursor failure. Rows and cursor roll back together;
   the later pull retries from the still-committed cursor.
5. Trace a tombstone through a third replica. Never convert a versioned deletion into
   absence/version 0 merely because the current UI displays neither.

## Contracts and deliberate limits

All four value kinds use strict expected-version comparison. Reading progress is an
explicit position from 0 through 100, not a maximum-completed score. Bookmarks use
stable opaque locations. Drafts are bounded plain demonstration text, not the full
Chapter 15 request DTO and never an order. Favorites use `saved` or a null tombstone.
Even apparently identical stale writes can conflict. The chapter teaches detection
and explicit resolution before proposing domain-specific merge policies.

A pass pushes at most 32 captured pending edits and pulls at most 100 changes. Repeat
explicitly when suitable; no endless background loop is installed. `MoreRemoteChanges`
only describes the downloaded page, not outstanding local work. A second local edit
for an unsettled key is rejected; preserve the user's editor buffer and settle that
key first. `ResolveAsync` waits for the worker before replacing a conflicted operation.
The model supports one owning replica instance per file, not multiple processes.

The server's receipts, rows and event list live only in memory and have no compaction.
It is unsuitable for an unbounded or publicly accessible service. Losing fixture
history requires an explicit reset of disposable replicas; never silently reset a real
customer's cursor. Durable server transactions/receipts, retention, cursor epochs,
bootstrap snapshots, API validation and real authorization are future integration work.
Tombstones cannot be pruned safely without a protocol for older offline devices.

Account labels and per-file checks test partition logic only. A production service
must derive its account from validated authentication and check ownership server-side.
Do not add a public endpoint accepting an account ID and call that authentication.
SQLite APIs perform synchronous work; this lab makes no native UI responsiveness claim.

## Verification

See `docs/verification.md` for executed results and exact revisions. Unrecorded native,
HTTP/TLS, identity-provider, server-restart and production checks remain pending.
