# Part I lab — retrieve, trace, test and diagnose the starter

This guide supports revised Chapters 1–5. Keep it open while using a separate exercise
checkout. The historical starter does not contain this newly written guide.

## Outcome and matching source

You will identify the Part I source, run its portable baseline, trace its first-page
search and perform a deliberate local pricing regression. Android launch is a
separate optional hardware/toolchain step, not a requirement for the portable lab.

The starter checkpoint is 0b8973660cc21269ff79eb54733edaf428fb5dac. Its recorded
verification is in that revision's docs/verification.md: 37 portable cases and
Android compilation. Device execution and iOS compilation remain unverified.
The later Chapter 18 code has 245 portable cases and different startup/navigation.
Use its own verification record rather than applying this lab's count to it.

## 1. Retrieve a dedicated exercise copy

Choose a new folder and preserve existing work:

```powershell
git clone https://github.com/minnesotabob/csharp-mobile-app-code.git csharp-mobile-app-code-part1
Set-Location csharp-mobile-app-code-part1
git switch -c reader/part-01 0b8973660cc21269ff79eb54733edaf428fb5dac
git rev-parse HEAD
git status --short
dotnet --info
```

Expected: the exact checkpoint ID, no status entries in a fresh copy, and an SDK
selection satisfying global.json. If the branch/folder already exists, inspect it
or select a new exercise location; do not force away changes.

## 2. Establish a portable baseline

```powershell
$tests = 'tests/GnazzosWorld.Core.Tests/GnazzosWorld.Core.Tests.csproj'
dotnet build $tests -c Release
# Continue only after this build succeeds.
dotnet run --project $tests -c Release --no-build
```

Expected at the matching checkpoint: 37 passing cases, no failures/errors/skips.
Record what you actually observe. If you get 245 passing cases, compare the source
revision before changing a test. If compilation fails, an older test executable
cannot establish that the changed source works.

## 3. Trace the first-page data path

| Step | Complete file in the starter | Question |
|---|---|---|
| Register dependencies | src/GnazzosWorld.App/MauiProgram.cs | Who supplies the catalog and creates MainPage? |
| Create the window | src/GnazzosWorld.App/App.xaml.cs | Which page is actually displayed? |
| Wire the UI | src/GnazzosWorld.App/MainPage.xaml | Where are CatalogView and OnSearchChanged declared? |
| Publish results | src/GnazzosWorld.App/MainPage.xaml.cs | Which assignment changes ItemsSource? |
| Apply portable rules | src/GnazzosWorld.Core/Catalog/CatalogSearch.cs | How are blank text, accents, filters and ordering handled? |
| Load fixture | src/GnazzosWorld.Core/Catalog/CatalogLoader.cs | Who owns the stream and validates data? |

Worked expectations: blank text returns five records; BERET identifies
song-minnesota-bob-beret-violet; KAI identifies book-kai-01; an unknown term returns
none. The native page should display an empty message for no matches and restore
five records when cleared. These are expected results, not new device observations.

If native debugging is available, break in the loader, constructor and search
handler. Compare the computed IDs with the actual control update. Record the
Android target, OS, source and observation; retain pending entries for untested
keyboard, large-text, screen-reader, restart and iOS behavior.

## 4. Perform the pricing regression on your exercise branch

Read tests/GnazzosWorld.Core.Tests/PricingTests.cs and
src/GnazzosWorld.Core/Orders/SongPricing.cs first. Work out the two-song amount from
the stated historical rule: 179.00 + 161.10 = 340.10 USD.

1. Save a successful baseline and the original factor.
2. Change only quantity two's cumulative factor from 1.9m to 2m.
3. Predict 358.00 as the wrong amount. Build Release again.
4. Run the suite. Second_song_gets_ten_percent_discount should detect the mismatch.
5. Restore the original factor, rebuild, rerun, and inspect git diff to preserve
   unrelated changes.

Worked answer: the implementation lost the second-song discount. Changing the
expected test amount to 358.00 hides the defect; it does not fulfill the rule.
This red/green procedure is a reader exercise. The editorial pass did not execute
this deliberate defect on a local Windows machine or claim new CI evidence.

## 5. Diagnose one screen defect separately

With a working Android environment, temporarily make the existing OnSearchChanged
handler empty while keeping its signature and XAML event name intact. Predict:
compilation can pass, portable search can pass, but typing does not update the cards.
Confirm the event breakpoint and missing result assignment, then restore the saved
handler and repeat the native check. Do not change the search algorithm to repair
an omitted UI assignment.

Without a native target, trace the same boundary in source and leave execution
pending. The right learning outcome is identifying what evidence is still needed.

## Completion record

Record source ID, local edits, selected SDK, Release build result, portable totals,
and any Android/iOS observations separately. Explain one result using its failed
boundary. Continue to the Part II milestone from docs/milestones.md only when you
understand why its startup and test totals differ from this starter.
