# Chapter 20: offline catalog and durable refresh lab

Start: Chapter 19 at `12c9ad41d761a34619a404f803833daa44931448`.
Implementation and executable tests: `74d2df5ade5b1d69b306bdb1a406722d08057ed1`.
Use the .NET SDK pinned in `global.json`.

## Build and run

```powershell
$p = 'tests/GnazzosWorld.Core.Tests/GnazzosWorld.Core.Tests.csproj'
dotnet build $p -c Release
dotnet run --project $p -c Release --no-build
```

This runs real-file SQLite checks plus the existing portable suite. No emulator,
public endpoint, customer data or credentials are required.

## Follow the implementation

1. Open `src/GnazzosWorld.Core/Storage/LocalCatalogDatabase.cs`: migration 2 -> 3
   adds nullable retrieval time and a singleton pending-refresh row. Earlier state
   is preserved. Legacy replacements clear freshness metadata.
2. Open `LocalCatalogDatabase.Offline.cs` in the same folder. Follow the ticket
   upsert, coherent snapshot reader, and transactional save/conditional acknowledgment.
3. Open `src/GnazzosWorld.Core/Offline/OfflineCatalogService.cs`. Local reading never
   fetches. The host explicitly requests work, then runs one bounded attempt.
4. Open `tests/GnazzosWorld.Core.Tests/OfflineCatalogTests.cs`. Read each assertion
   before running. The fake clock makes the 24-hour boundary deterministic.

## Worked checks

- First launch: bundle returned, unknown freshness, zero fetch calls.
- Restart: persist intent, dispose store, reopen same path, then run worker;
  saved document and matching queue acknowledgment survive.
- Network failure: earlier revision and intent remain; failure classification is explicit.
- Cancellation: propagates without falsely reporting completion.
- New request during fetch: newer ticket remains after older worker completes.
- Failed queue delete: SQLite trigger rejects acknowledgment; snapshot write rolls back too.
- Clock: exactly 24 hours is stale; negative/unknown age is stale; no sleep needed.
- Single coordinator: overlapping worker calls fetch once if no newer request arrives.

## Guided modification

In a disposable checkout, remove `AND ticket=$ticket` from the acknowledgment SQL.
The newer-request test must fail. Restore the predicate: an older operation may
only acknowledge the request generation it owns.

## Independent extension

Add a persisted retry cooldown with injected time. Classify retryable failures;
do not drain a permanent invalid-payload response in a tight loop. Include restart,
clock correction and explicit user retry cases.

## Limits

One coordinator owns one store; independent workers need database leasing/write
arbitration. This is a public GET queue, not a customer-order outbox. No automatic
retry scheduler, MAUI browser wiring, native job registration, phone runtime,
TLS/device or iOS verification is claimed. The original browser remains bundled.
Microsoft.Data.Sqlite async operations can execute synchronously; native adapters
must measure and manage UI-thread work explicitly.
