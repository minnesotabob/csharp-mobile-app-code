# Reader learning guide

This companion keeps the book's intermediate/advanced scope. Its purpose is to help
you understand, run, diagnose and extend the application. A chapter listing is an
explanation of a complete project, not the whole project by itself.

## Start from a recorded working revision

The Chapter 18 implementation verified in docs/verification.md is
5c24e63dffd494463716730fdf553af74453a2ae. It passed 245 portable cases, 26 API
integration cases, probe compilation, a local Kestrel HTTP smoke and Android Debug
compilation. Follow-up documentation is available on the current review branch.
Read docs/milestones.md before selecting an earlier source milestone. Do not assume
one verified per-chapter tag exists; no complete set of chapter tags is published.

Use the repository root for the following commands and inspect global.json before
installing tools. Run dotnet --info to compare your environment with the recorded
one. Windows portable and API checks do not require a mobile workload.

```powershell
dotnet build tests/GnazzosWorld.Core.Tests/GnazzosWorld.Core.Tests.csproj -c Release
dotnet run --project tests/GnazzosWorld.Core.Tests/GnazzosWorld.Core.Tests.csproj -c Release --no-build
dotnet build tests/GnazzosWorld.Api.Tests/GnazzosWorld.Api.Tests.csproj -c Release
dotnet run --project tests/GnazzosWorld.Api.Tests/GnazzosWorld.Api.Tests.csproj -c Release --no-build
```

At the recorded revision, expect totals of 245 and 26 respectively, with zero
failures, errors, skipped or not-run cases. Later chapters will change these totals;
compare against the verification record for your selected revision. Read a failing
case's name and assertion before changing its expected result.

## Learning paths and sample map

| Reader task | Source to inspect | Executable checks / next instructions |
|---|---|---|
| Validate catalog meaning and identities | src/GnazzosWorld.Core/Catalog | CatalogTests.cs and CatalogMetadataTests.cs |
| Trace UI state, navigation and adaptive decisions | src/GnazzosWorld.Core/Presentation | PresentationTests.cs; docs/device-checks.md for native observations |
| Search, sort and persist favorites | Catalog/CatalogSearch.cs, Catalog/Favorites.cs, Presentation/CatalogBrowserViewModel.cs | DiscoveryTests.cs |
| Open details and handle handoff failures | Presentation/DetailViewModel.cs and Catalog/ContentLinks.cs | DetailJourneyTests.cs |
| Build and review a song request | Orders/SongRequest.cs and Presentation/SongRequestViewModel.cs | SongRequestTests.cs |
| Price and retry one simulated submission | Orders/SongPricing.cs, Orders/SongSubmission.cs and Presentation/SongSubmissionViewModel.cs | PricingTests.cs and SubmissionTests.cs |
| Bound and validate an HTTP response | Networking/HttpCatalogSource.cs and Networking/BoundedJsonClient.cs | HttpCatalogTests.cs |
| Validate a versioned catalog snapshot | Contracts/CatalogDocument.cs | CatalogDocumentTests.cs |
| Run the API and understand its wire contract | src/GnazzosWorld.Api and tools/GnazzosWorld.CatalogProbe | docs/local-api.md and docs/labs/ch18-api.md |

Unless otherwise qualified, paths in the map are relative to src/GnazzosWorld.Core,
and portable test filenames live under tests/GnazzosWorld.Core.Tests. API pipeline
checks live in tests/GnazzosWorld.Api.Tests/CatalogApiTests.cs. Read the complete
file and its registrations/callers alongside a short manuscript excerpt.

## A repeatable way to study a sample

1. State the expected behavior in your own words and run its unchanged baseline.
2. Locate the input model, rule/use case, adapter, caller and meaningful test.
3. Predict what will happen for one normal input and one failure or cancellation.
4. Trace those paths through the source. Identify who owns state and resource cleanup.
5. Make one isolated change in your own branch and rerun the relevant checks.
6. Explain the result, restore a working state and record what the test did not prove.

When changing a pricing rule, first distinguish a historical price fixture from a
newly approved business policy. When studying retries, examine a response lost
AFTER acceptance as well as a failure before acceptance. These are different cases.

## What is simulated and what actually ran

| Capability | Current implementation / evidence | Additional work needed |
|---|---|---|
| Portable rules and state | Executed xUnit suites | Native UI observations |
| Main mobile catalog | Bundled fixture | Remote refresh/freshness and persistence work |
| Native networking lab | In-process HTTP handler | Real socket/TLS/device integration |
| Song quote/submission | Process-memory simulation | Durable ownership, authenticated service and later payment workflow |
| Public catalog API | Real ASP.NET Core pipeline plus loopback HTTP startup check | Chosen HTTPS endpoint and actual device checks |
| HTTPS catalog probe | Builds; real endpoint instructions supplied | Reader-specific certificate/endpoint run |
| Android | Debug compilation passed | Emulator/hardware execution and UX checks |
| iOS | Planned platform coverage | Build-host setup, compilation and device checks |

A fixture is useful when it isolates the concept under study. Keep its label and
limits visible; do not turn a simulated receipt into a paid order or infer native
behavior from a successful portable test.

## Troubleshooting before editing code

| Symptom | First check | Next action |
|---|---|---|
| SDK selection fails | Compare dotnet --info with global.json | Install the pinned supported SDK; keep project versions deliberate |
| No expected test executable | Was the matching Release project built? | Build the project first, then rerun with --no-build |
| Different test totals | Compare source revision with docs/verification.md | Use the matching milestone; do not silently discard tests |
| API probe reports HttpStatus | Inspect status and route, then docs/local-api.md | Correct endpoint/version/query; do not treat an error as an empty catalog |
| API probe reports Network | Check listener, DNS, TLS hostname and trust separately | Verify the chosen endpoint without disabling certificate checks |
| App lab succeeds while offline | Read the lab's in-process label | Use real endpoint/device checks to establish internet or service reachability |
| Windows API works, phone fails | Determine where localhost resolves for that device | Follow the device networking map in docs/local-api.md |

## Guided practice and chapter status

Start with [the Part I foundation lab](labs/part01-foundation.md) for Chapters 1–5,
using its dedicated historical starter checkout and 37-case baseline. Chapters 1–18
of the manuscript now have an initial instructional revision with learning objectives,
guided exercises and chapter summaries. Chapters 6–18 contain guided labs in the
manuscript itself; a dedicated companion lab document has not been created for every
chapter. Use [Chapter 18's API lab](labs/ch18-api.md) for the later API milestone.

This is **not** a claim that all chapters have completed a technical source audit.
The 8 × 10 PDF proof predates the newest text edits, and native Android/iOS behavior
still needs recorded device evidence. Treat the archived CI counts as revision-specific
results, not new verification of every revised manuscript instruction.
