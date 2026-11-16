# Chapter 37: performance, reliability and accessibility investigation

This lab measures real Release desktop work and supplies a separate native stress page.
Exact code/build/workflow evidence is in verification.md. No phone benchmark, decoded
image memory, frame-rate, battery, screen-reader or NativeAOT result is fabricated.

## Reproduce the desktop investigation

Use the pinned SDK 11.0.100 without a debugger. From a fresh checkout/workspace:

```powershell
dotnet publish src/GnazzosWorld.Api -c Release -o artifacts/api
dotnet build tools/GnazzosWorld.PerformanceLab -c Release
$testedRevision = git rev-parse HEAD
dotnet run --project tools/GnazzosWorld.PerformanceLab -c Release --no-build -- artifacts/performance-evidence.json artifacts/api/GnazzosWorld.Api.dll src/GnazzosWorld.App/Resources/Raw/performance-images.json $testedRevision 0
```

Run one command at a time and stop after failure. Choose a new output path on every
run; 0 denotes a local run, not a GitHub observation. The workflow supplies its own
checked-out SHA/run ID. The report uses only synthetic/public observations and omits
raw request bodies, process logs and arbitrary environment variables. The public API
child is stopped in finally. An unavailable loopback port or process failure fails the
run rather than producing a timing claim. The released port selection has a small
bind race; rerun in a fresh workspace after diagnosing a port conflict, not to select
the fastest successful number. No public host or machine trust configuration is changed.

The report records OS/runtime/architecture/logical processor count, source/run, all 21
search/API samples, GC counts, current-thread allocations, construction cost, controlled
retention observations and image fixture dimensions/hash-checked encoded sizes. It
keeps native startup/scrolling/decoded memory, energy/wire costs, accessibility and
NativeAOT not_run. The GitHub artifact expires after seven days; the observed safe
JSON is retained at reviews/ch37-desktop-evidence.json with its original identity.

## Observed baseline and change

Source c82eff270ec50c3ef4916c4401576ba1b893b1a9, workflow 43876553256, Windows
10.0.26100, .NET 11.0.0, X64, four logical processors, GitHub-hosted shared VM:

| Metric | Reference filter + sort | Reused sorted snapshot |
| --- | ---: | ---: |
| Median five-query batch | 107.7073 ms | 93.4096 ms |
| Nearest-rank p95 batch, 21 samples | 128.6835 ms | 121.0163 ms |
| Median allocated bytes per batch | 633,032 | 140,896 |

Both use the same 10,000 deterministic records and five queries: synthetic, cafe,
beret, Japanese text and no-match. Three paired warmups precede 21 alternating-order
pairs. Every query's complete ordered ID sequence is checked before measuring; result
counts are consumed in timed batches. No arbitrary wall-clock pass threshold or forced
GC is used between search samples. The retained JSON includes GC observations and
outliers; do not report only the fastest repetition.

Allocation falls 77.74%; the median time reduction is 13.27% in this observation. That
is a modest timing change on a noisy shared desktop, not a guaranteed mobile speedup.
The first snapshot construction separately costs 28.8451 ms and allocates 1,196,968
bytes, including first-use/JIT work. It is excluded from warmed batches, not free. The
snapshot keeps two reference orderings; it does not cache an unbounded set of queries.
Matching is still linear and accent-insensitive. The remaining cost needs a CPU trace
before another optimization. Do not infer that sorting accounted for every UI stall.

The published API child reached its first successful health response in 614.9966 ms,
with 100 ms polling. This is fresh-process-to-health under existing OS caches, not
cold native startup or first usable screen. After three warmups, 21 sequential local
public-catalog HTTP/body reads have median 0.3119 ms, p95 0.3419 ms, and body size
2,731 bytes. It is a five-record public fixture, one client, shared host and no TLS/WAN
load. It does not establish server capacity, customer-route latency, egress or battery.

## Source walkthrough and tradeoffs

CatalogSearch.Find remains the reference path. CatalogSearchSnapshot copies trusted
immutable record references and constructs title/newest orderings once. Both paths
share CatalogSearch.Filter, preserving validation, accent matching, creator/kind/favorite
intersection and deterministic ties/unknown-date ordering. The snapshot's result array
is caller-owned and cannot modify its private indexes; Items is a read-only wrapper.
Create a new snapshot for a new catalog revision. This is not a mutable incremental
search index or a new cache of private/customer content.

CatalogBrowserViewModel now uses one stable snapshot and avoids a redundant initial
result before ReloadFavorites rebuilds. Selection/clear/navigation semantics are tested.
The main bundled catalog remains five records. Its synchronous rebuild is not proof
that a 10,000-row search belongs on the UI thread. The separate stress page runs fixture
preparation and search off the UI thread, then checks generation/visibility before
publishing on the UI continuation. Changing query, reloading or leaving invalidates
old work; a task can still consume CPU after invalidation. Bound work and page data.

The eight new portable cases include bounded deterministic fixtures, both sort orders
across queries/filters/favorites, shallow snapshot ownership/result mutation, invalid
filters/empty input and view-model selection. Existing discovery assertions remain.
Correctness checks are separate from timing evidence; no test asserts a fixed millisecond
budget on a shared runner.

## Controlled retention investigation

RetentionMeasurements deliberately omits disposal on 32 actual BackgroundPlaybackOwner
instances, each with a synthetic 64 KiB closure. The long-lived fake player holds their
Observed delegates. Forced full collections in this diagnostic experiment observe 32
owners still reachable. Disposing the owners removes subscriptions and Changed closures;
subscriber count changes from 32 to 0 and observed weak-owner count from 32 to 0.

Only subscriber counts are required invariants. Weak counts depend on GC/JIT lifetime
and are reported observations. GC.Collect is not a production cleanup strategy or a
performance fix. This demonstrates an omitted-cleanup failure using existing owner
code; it does not assert that an actual native page leaked. Inspect page Window/Shell
hooks, dispatcher timers, native handlers and the matching detach/dispose path. A
self-contained managed cycle is not itself a GC root; long-lived publisher paths matter.
Physical erasure of media/private memory is not proved by a collected weak reference.

## Native stress page and image fixtures

Home -> Catalog performance lab -> Load 10,000 synthetic rows. Compare Thumbnail
96 x 112 and Full image 1200 x 1400 on the same Release package/device. Both are original
procedural PNGs from eng/generate-performance-images.py; no external artwork is used.
They are MauiAsset raw resources so the build does not pre-resize both through MauiImage.

| Variant | Encoded bytes | RGBA plane estimate (width x height x 4) |
| --- | ---: | ---: |
| Full 1200 x 1400 | 13,263 | 6,720,000 bytes |
| Thumbnail 96 x 112 | 7,112 | 43,008 bytes |

The pixel-plane ratio is 156.25, while the PNG byte ratio is only about 1.86. PNG bytes
are not decoded memory. The plane estimate excludes stride, formats, downsampling,
caches, duplicate buffers and GPU resources; it is not measured native memory. The
platform may decode smaller or reuse cached resources. The lab supplies a new stream
per image request; do not return one already consumed/disposed stream.

The CollectionView has a finite Grid star-row viewport and variable-height labels.
It is not nested in the controls ScrollView. Template construction count is cumulative
since load, not current live/native cell count or proof of virtualization. No fixed row
height/MeasureFirstItem shortcut is imposed on long multilingual large-text rows. Data
virtualization is separate: all generated metadata remains in managed memory. Leave
clears sources/template/snapshot and fences old results, but native reclamation still
needs device observations. Mode changes reload the same deterministic metadata.

## Device protocol — not yet executed

Use reviews/ch37-device-accessibility-review.md and the Chapter 36 candidate record.
Name physical device/model/OS, CPU/memory class, display refresh, app/API/schema/package
hash, Release/runtime/trim/AOT configuration, locale/text scale, assistive mode, network,
battery/charging and thermal conditions. Do not replace actual models with generic
Android/iPhone slots when recording a result.

1. Define startup boundaries: launcher action to first frame and to first useful catalog
   interaction. Distinguish terminated-process launch, first install/upgrade and warm
   foreground return. State cache policy; a new process is not a cold OS/filesystem cache.
2. Repeat fixed journeys and keep all runs, median/p95 and variability. Do baseline and
   candidate on the same device/settings; alternate order and cool down if thermal
   state changes. Measure profiler-off runs separately from diagnostic traces.
3. Scroll the same distance/speed through the 10,000-row fixture, in both image modes,
   with long text, large font, narrow/tablet layouts and screen readers. Capture actual
   frame timing/jank and peak/settled native/managed memory with platform tools.
4. Navigate load/search/leave repeatedly. Inspect publisher/timer/handler retention paths,
   native image/player handles, exceptions and unconfirmed cleanup. Compare like states
   after a controlled idle interval, not one arbitrary Task.Delay followed by a heap size.
5. Measure API start/client/server phases and a real network separately. Count body bytes,
   retries, range resumes and cache hits. Use platform network/energy tools for actual
   wire/radio/CPU/wake-up effects; bytes alone do not determine battery drain or cost.
6. Review focus/reading order, meaningful labels, error/status announcements, contrast,
   keyboard, reduced motion, large text and localization. Keep failures/blocked/not_run
   rows visible; a faster page that clips or loses focus is not an acceptable improvement.

## Profiling, .NET 11 runtime and trim/AOT review

Current official .NET 11 documentation makes CoreCLR the default mobile runtime and
rejects UseMonoRuntime=true. NativeAOT is an opt-in publish model; Android support is
experimental. Do not copy old Mono defaults or disable analyzers to hide dependencies.
Inspect the pinned SDK/workload's actual build/runtime and recheck documentation.

Official profiling setup uses dotnet-trace/dotnet-dsrouter for Android/iOS and records
tool versions. Example physical-Android collector in terminal one, then deploy from the
repository root in terminal two (requires a connected configured device; not executed):

```powershell
dotnet-trace collect --dsrouter android --format speedscope
dotnet build src/GnazzosWorld.App -t:Run -c Release -f net11.0-android -p:DiagnosticAddress=127.0.0.1 -p:DiagnosticPort=9000 -p:DiagnosticSuspend=true -p:DiagnosticListenMode=connect
```

Use DiagnosticSuspend=false to attach after startup for runtime/heap work. Follow the
official guide's ios/macOS prerequisites and listen-mode settings for an Apple target.
Diagnostic endpoints/dumps can expose process data; keep them local/protected, never
ship a public diagnostic port, and apply Chapter 35's access/retention rules to traces.
Capture a trace to locate matching/sorting/serialization/UI/native decode work rather
than assuming every long frame is a GC pause. Record tracing overhead separately.

No full-trim or NativeAOT compatibility is asserted. AccountPage and SongRequestPage
still contain string-path bindings, and FileRequestDraftStore plus portable clients
use reflection-based JSON. Third-party auth/media SDKs need their own compatibility
review. Prefer compiled XAML/typed bindings and source-generated serialization where
needed; fix trim/AOT diagnostics, publish the actual target and test the published app.
Changing flags alone is not an optimization result. Chapters 38–39 retain signing and
install/upgrade work, with no iOS result supplied here.

Primary references checked November 16, 2026:
https://learn.microsoft.com/en-us/dotnet/maui/deployment/performance;
https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/profiling;
https://learn.microsoft.com/en-us/dotnet/maui/deployment/runtimes-compilation?view=net-maui-11.0;
https://learn.microsoft.com/en-us/dotnet/core/compatibility/maui/11/mobile-runtime-replacement;
https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/collectionview/populate-data;
https://learn.microsoft.com/en-us/dotnet/maui/deployment/trimming;
https://learn.microsoft.com/en-us/dotnet/maui/deployment/nativeaot;
https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/accessibility;
https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html.
