# Device checks — pending execution

Record device/OS, exact source/build, date, observed result and defect evidence. No check below is marked complete by the Windows compile or portable tests.

1. Launch offline, navigate Home to Discover, select each kind and open details. Test phone back, tab changes, repeated taps and unknown/invalid IDs.
2. Search BERET, clear it, enter an unknown query, and filter with a selected record. Confirm hidden selections cannot show stale detail content.
3. Resize around widths 899/900 units. Confirm narrow full-page details versus wide inline details, retained selection, rotation and split-screen behavior.
4. Increase text/display size and open the keyboard. Confirm buttons, picker, list and details remain reachable, including short landscape windows.
5. Verify TalkBack/VoiceOver order, labels, headings, selected states and explicit Open details action. Ensure decorative artwork is not announced.
6. Check system light/dark themes and selected/unselected contrast. Repeat with large text and platform contrast settings.
7. Start in English and Spanish, restart after changing the platform language, and confirm neutral fallback, singular/plural summary, text expansion and no clipped controls. Runtime language switching is not implemented.
8. Repeat on Android emulator/hardware and, after a successful iOS build, iPhone/iPad. Check background/resume and cold restart separately.
9. Build Release and repeat critical navigation/search/layout checks; examine logs for private data. Do not present Debug compilation as release verification.

Multiwindow, RTL, fold hinge geometry, remote image pipelines and persisted state need later dedicated implementations/checks. MFA, notifications, media, account deletion, payments and upgrades will add separate scenarios when implemented.

## Chapter 13 additions — all pending execution

10. Type k, ka, kai quickly; clear immediately; type again and navigate away before 250 ms.
    Return and confirm the last typed query is retained, with no late stale update.
11. Open Filters, combine content type and creator, change sort, then collapse the panel.
    Test keyboard-open landscape, large text, Spanish expansion, focus order and scrolling.
12. Select an item and save a favorite. Enable Favorites only and remove that selection.
    Confirm the result, selection, inline detail and Open selection action reconcile.
13. Save favorites, terminate and restart the app, and confirm the same IDs load.
    Change fixture titles without changing IDs and verify favorites still resolve.
14. In a controlled development fixture, make stored favorites unreadable. Confirm ordinary
    browsing works, the error is visible, edits are disabled and Reload favorites can recover
    after the fixture is repaired. Do not overwrite real preferences merely to run this check.

## Chapter 14 additions — pending

- Open book details and the teaching sample; check paragraph breaks, credit and scrolling.
- Verify metadata/sample/action focus order with large text and Spanish.
- Open the listed website and return; preserve detail context on phone and wide layout.
- Verify unavailable and missing records show no stale sample or website action.
- Exercise a failed native opening if possible; check the message is discoverable.
- Do not infer external-browser close/cancel from the Boolean launch result.

## Chapter 15 additions — all pending execution

- Home: open custom-song form, complete three steps, and inspect every review field.
- Turn vocals off/on and preserve draft notes; instrumental review omits vocal notes.
- Leave/return before saving; preserve open edits and step within the same process.
- Save, edit without saving, navigate away/back, then terminate/relaunch. Distinguish
  in-process edits from the last explicitly saved draft after a restart.
- Fail save/delete through an injected store; preserve open values and report retry.
- Empty each required field; invalid Next stays on the same step without data loss.
- Review then change a field; hide the old review until validation succeeds again.
- Exercise discard confirmation and Keep; verify saved/open values on both paths.
- Check keyboard-open story/contact inputs, narrow landscape and large text.
- With TalkBack/VoiceOver, verify hints, step heading, error announcements and review
  reading order. Field-specific focus routing is not implemented in this milestone.
- Use synthetic personal data. Check actual local-file restore/corruption behavior
  separately from portable store-double tests. New form strings are English only.

## Chapter 16 — pricing and simulated submission (pending native checks)

- Quote one, two, and three songs; check the explicit USD amounts and version.
- Edit quantity, story, and email after review. Ensure review and quote actions
  disappear and existing writing remains. Re-review and request a fresh quote.
- Obtain a quote, wait until its service expiry, submit, then explicitly requote.
- Tap rapidly with an injected delayed service; check disabled controls, navigation,
  focus and screen-reader announcement behavior on Android and iOS.
- Inject a response loss after acceptance; retry must use the same operation and
  display the original receipt. No new quote or editing while unresolved.
- Navigate away/back after uncertainty and acceptance. State survives navigation
  in this single process. Restart loses all demo quotes/receipts/pending operations.
- Test phone/tablet, rotation, large text, dark theme and keyboard reachability.
  New form/quote strings remain English; localization/focus refinement is pending.
- Use fictitious contact/story details. There is no live transmission or payment.

## Chapter 17 networking lab — native checks pending

- Open the lab from Home on Android and iOS; labels must identify an in-process
  demo, not a test of internet access. Main catalog browsing remains bundled.
- Run the fixture and check five validated records. Repeat with airplane mode;
  local transport still runs, and the connectivity hint must not imply reachability.
- Inject a delayed handler, leave the page, return and run again. Observe that
  cancellation/late results do not publish success to an obsolete visit or leave
  the button disabled. Try repeated taps, large text, rotation and screen readers.
- New lab strings are English; full form/lab localization and focus refinement
  remain pending. No accessibility-runtime claim follows from compilation.
- When a real Chapter 18 endpoint is configured, separately test local addressing,
  TLS trust, redirect policy, Wi-Fi/cellular changes, captive portals, rate limits,
  socket timeouts and interrupted bodies. Handler tests do not prove these.

## Chapter 18 — real endpoint integration (pending device observations)

- Follow docs/local-api.md to run the workstation API and HTTPS probe. Record the
  chosen hostname, certificate trust, source revision, schema/revision and counts.
- Establish endpoint reachability on the actual Android emulator/phone or paired
  Mac/iOS device. localhost is not the Windows workstation in every environment.
- Check exact TLS hostname/trust, request host filtering, redirect behavior, network
  permission and canceled reads. Do not add blanket trust or cleartext exceptions.
- If deliberately using a public-fixture development tunnel, use the exact printed
  HTTPS endpoint and chosen access policy, then stop hosting after the exercise.
  No tunnel, listener exposure or service deployment is created by this milestone.
- Main catalog/native lab are not switched onto the real API here. TestServer
  integration and the loopback HTTP smoke do not prove native HTTPS connectivity.

## Chapter 27 - foreground audio (all native observations pending)

Use docs/labs/ch27-audio-progress.md. Current expanded app minimum is Android API 26;
MediaElement is pinned to 12.0.0. Original bundled WAV fixtures are 10/14 seconds.
Record source/package/device/OS, native state, audible result and persisted checkpoint.
Check load without autoplay, Play, pause/save/reload, seek rounding/endpoints, replay,
manual next/previous, unavailable resource, native failure, navigation/background during
open/seek, save failure/reset, text size and screen reader focus. Recheck hosted browser
return after the activity single-task setup change. iOS build/runtime, memory cleanup,
phone/audio-focus/headphone and background-system behavior are separate pending work.

## Chapter 28 background audio

Use [background-audio-lab.md](background-audio-lab.md). Every native worksheet row remains pending. Android compile/merged manifest and simulated player tests do not verify actual system controls, focus, routes, calls or power behavior. iOS compilation is also pending.

## Chapter 29 public download acceptance

Use download-lab.md. Every Android/iPhone row remains pending: HTTPS reachability and
trust, cancellation timing, process restart, cache eviction, low-space/write behavior
and deletion. Portable injected-space failures, ASP.NET pipeline tests and real loopback
HTTP range/hash checks do not establish native networking or filesystem behavior.
