# Source milestones

## Part I foundation
Use commit 0b8973660cc21269ff79eb54733edaf428fb5dac to follow Chapters 1–5. It includes the first MainPage, NavigationPage startup, 37 passing portable tests, and recorded Android build evidence.

https://github.com/minnesotabob/csharp-mobile-app-code/tree/0b8973660cc21269ff79eb54733edaf428fb5dac

## Part II interface
The following commits develop Shell, portable view models, reusable views, localization, and width-based list/detail presentation. See docs/verification.md for the exact verified revision. Part I source remains available in history; current main intentionally has different startup/navigation.

No app-store release, runtime device validation, or chapter tag is implied by these source milestones.

Part II verified implementation: 9497b0d7e8acb74c07c50be8cb56f26453664fbb. Windows CI passed 61 portable tests and Android Debug compilation; native device and iOS verification remain pending. See verification.md for exact run links.

## Chapter 13

- Source: 51870ac298eff6198e098a063bdc391fecc665f8.
- Portable suite: 112 passing tests; Android Debug build passed.
- Adds combined discovery criteria, controlled input debounce and bounded device-local favorites.
- See verification.md for CI links; native execution and iOS remain pending.
