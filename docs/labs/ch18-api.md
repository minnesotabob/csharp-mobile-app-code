# Guided lab — trace a public catalog from API to validated client

Outcome: run the local fixture service, consume it with the real HTTPS document
client, reconstruct one paged snapshot and explain three distinct verification levels.

Prerequisites: the Chapter 18 source recorded in ../verification.md, its pinned
Windows SDK, PowerShell and a trusted workstation development certificate. This lab
uses public fixtures only. Native device/tunnel configuration is a separate exercise.

## 1. Establish the unchanged baseline

At the repository root, build/run the portable and API suites using ../learning-guide.md.
Expected results at revision 5c24e63dffd494463716730fdf553af74453a2ae: 245 portable
cases and 26 API cases, all passing. If they differ, identify the selected revision
and resolve the baseline before experimenting.

Read src/GnazzosWorld.Api/Program.cs and PublicCatalog.cs. Locate the registration
that loads bundled data, the fixed revision and the ordinal ID sorting. Predict
whether the main phone catalog has changed source: it has not; it remains bundled.

## 2. Start the real service

Use ../local-api.md for certificate setup and any workstation-specific failures.
In terminal A at the repository root:

```powershell
dotnet dev-certs https --trust
dotnet run --project src/GnazzosWorld.Api --launch-profile https
```

Leave terminal A running. In terminal B:

```powershell
dotnet run --project tools/GnazzosWorld.CatalogProbe -- https://localhost:7443/api/v1/catalog
```

Expected summary for the shipped fixture:

```text
Schema 1; revision fixture-2026-11-15-v1; 5 items; 3 creators.
```

Worked check: the API serializes a CatalogDocument, BoundedJsonClient enforces the
transfer policy, CatalogDocument.Read validates the envelope, and CatalogSnapshot.Create
checks creator references. The count alone does not establish those relationships;
trace the validation method and the matching integration test.

This expected HTTPS output is derived from the shipped fixture and compiled probe.
A real trusted-endpoint probe run has not been observed in the book's verification
record. Your successful run establishes your workstation integration; record it.

## 3. Reconstruct a page sequence

Predict the page sizes before running this loop: two, two and one. In terminal B:

```powershell
$catalog = Invoke-RestMethod 'https://localhost:7443/api/v1/catalog'
$offset = 0
$ids = @()
do {
    $url = 'https://localhost:7443/api/v1/catalog/pages?offset={0}&limit=2&revision={1}' -f $offset, $catalog.revision
    $page = Invoke-RestMethod $url
    $ids += @($page.items | ForEach-Object { $_.id })
    $offset = $page.nextOffset
} while ($null -ne $offset)
$ids
```

Expected IDs, in order:

```text
book-csharp
book-kai-01
podcast-demo-01
song-minnesota-bob-beret-violet
song-minnesota-bob-northern-lights
```

Worked check: IDs use ordinal sorting, not titles or artist display names. Each
continuation supplies the original revision. A null nextOffset ends the loop; it
is not a command to restart at zero. CatalogApiTests.Continuing_pages_reconstruct_same_order_without_duplicates
provides the executable pipeline check for this agreement.

## 4. Predict and inspect failures

For each suffix below, predict the result, then inspect it in your HTTP client or
trace the named API test. The service base is https://localhost:7443.

| Request suffix | Expected result | Reason / executable check |
|---|---|---|
| /api/v1/catalog/pages?limit=0 | 400 validation problem | Limit must be 1..100; Invalid_query_returns_validation_problem |
| /api/v1/catalog/pages?limit=1&limit=2 | 400 validation problem | Duplicate values are ambiguous; same parameterized test |
| /api/v1/catalog/pages?offset=2 | 400 validation problem | Continuation requires the original revision |
| /api/v1/catalog/pages?offset=2&revision=older | 409 problem | Snapshot changed; Stale_page_revision_returns_conflict |
| /api/v1/catalog/pages?offset=5&revision=fixture-2026-11-15-v1 | 200, empty terminal page | Offset equals total; End_offset_is_empty_terminal_page |

Worked answer: fix an invalid query before retrying. For a revision conflict,
restart collection from the first page. Repeatedly issuing the same bad request
cannot establish a consistent snapshot. Never convert a failure into an empty
catalog and silently erase useful results.

Inspect Unexpected_exception_returns_safe_problem_in_all_environments. It verifies
that a marker in the thrown exception does not appear in the HTTP response. It does
not prove that all server logs are scrubbed or that an identity system is secure.

## 5. Compare verification boundaries and clean up

Read eng/Smoke-Api.ps1, then run it after the Release API build described in
../local-api.md. It starts a separate temporary loopback HTTP instance and stops
that instance afterward. Expected message:

```text
Local Kestrel HTTP smoke passed: health, catalog and pagination.
```

Worked answer:

- Portable tests establish model/transport policy in controlled conditions.
- WebApplicationFactory establishes the actual ASP.NET Core pipeline through TestServer.
- The Kestrel smoke establishes real local HTTP startup and basic contract responses.
- A successful HTTPS probe establishes the chosen workstation endpoint/certificate path.
- None of these establishes an Android/iPhone connection, native accessibility or iOS compilation.

Stop terminal A's service with Ctrl+C when finished. No tunnel or public deployment
is required for this workstation lab.

## Independent extension

Design an integration case for a three-item page followed by a two-item page. Write
down the expected offsets, revision use and final continuation before adding code.
Explain why the test should compare actual IDs as well as item counts. Keep the
existing fixture/revision agreement unchanged unless deliberately testing a new one.
