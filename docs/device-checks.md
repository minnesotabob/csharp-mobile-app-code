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
