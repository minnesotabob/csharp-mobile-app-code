# Chapter 37 device, accessibility and performance review

Disposition: **incomplete; native release acceptance not established**.
Source under review: c82eff270ec50c3ef4916c4401576ba1b893b1a9. Desktop observations are
recorded separately in ch37-desktop-evidence.json. No named physical device/package
has been exercised for this chapter. Proposed Android phone/tablet and iPhone/iPad
slots are not observations; do not attach pass results to unnamed environments.

## Source inspection performed

- Main catalog and stress list use finite Grid viewports with CollectionView; the stress
  list is outside its controls ScrollView and does not impose fixed text-row height.
- Main XAML catalog/card have x:DataType; stress card assigns typed values on binding
  context changes. Main account/request pages still have string-path bindings, an AOT
  review gap. No full-trim/AOT compatibility claim is made.
- Global labels enable automatic font scaling; decorative catalog/stress images are
  excluded from reading. Stress instructions/status are English lab text, not localized
  product strings. Actual accessibility order and expansion remain unobserved.
- Existing BackgroundPlaybackOwner unsubscribes Observed and clears Changed on Dispose.
  The controlled desktop omitted-Dispose experiment establishes that root path, not a
  discovered native leak. Page Window/Shell/timer and platform handlers need device review.
- Source palette arithmetic uses declared foreground/background values from App.xaml.
  It does not include disabled/focused/platform-rendered states, high contrast, overlays,
  images or screenshots. Raw ratios and color values are in ch37-palette-source.json.

| Declared pair | Computed contrast ratio | Source check |
| --- | ---: | --- |
| TextLight / PaperLight | 15.6542:1 | Above 4.5:1 for this declared pair |
| TextDark / PaperDark | 14.9679:1 | Above 4.5:1 for this declared pair |
| TextLight / SelectionLight | 13.3956:1 | Above 4.5:1 for this declared pair |
| TextDark / SelectionDark | 8.7059:1 | Above 4.5:1 for this declared pair |
| White / Accent | 8.0415:1 | Above 4.5:1 for this declared pair |

Ratios are rounded only for presentation; the decision used unrounded values. This is
source-color inspection, not a complete WCAG or native accessibility certification.

## Outstanding observations

For each row, record actual candidate hash, model/OS, configuration, date/tester,
steps/expected/actual result, protected evidence and defect. Do not overwrite not_run
with blocked/pass unless the corresponding prerequisite or observation is recorded.

| Review item | Proposed environment / expected observation | Result |
| --- | --- | --- |
| Terminated-process launch / first useful interaction | Same named Android phone and iPhone; defined start/end/cache policy, repeated distributions | not_run |
| Warm return / orientation / split screen | Named phone/tablet; preserve visible state and usable controls | not_run |
| Scroll frames / jank | Same route/data/gesture, profiler-off baseline/candidate and both image modes | not_run |
| Native/managed image memory | Named device; peak and settled memory, cache/downsample/resource behavior | not_run |
| Load/search/leave retention | Repeat route cycles; inspect event/timer/handler/native roots and old work | not_run |
| Offline/resume/low storage | Original operation/cache semantics, bounded retries, no partial success claim | not_run |
| Battery / network / thermal | Controlled charge/radio/brightness/temperature; actual platform counters | not_run |
| TalkBack reading/focus | Labels, order, search/status, navigation/error focus, reachable controls | not_run |
| VoiceOver reading/focus | Same journey on actual iPhone/iPad; independent result | not_run |
| Large text / keyboard / localization | English/Spanish and stress multilingual titles; no clipping, occlusion or lost actions | not_run |
| Light/dark/high contrast / disabled/focus states | Actual rendered text/control states, no color-only meaning | not_run |
| Reduced motion / media alternatives | User settings respected; meaningful text alternatives and controls | not_run |
| Release trim/AOT/package regression | Published target/dependencies with diagnostics resolved; install/upgrade and core journeys | not_run |

No performance improvement may be called release-approved until correctness,
accessibility and remaining acceptance gaps are evaluated against the same candidate.
