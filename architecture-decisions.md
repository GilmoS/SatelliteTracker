# Architecture decisions — standing checklists

Short, durable rules distilled from bugs that recurred. The full reasoning for each lives in the
`CLAUDE.md` files linked below; this file is the checklist to run through when building or
changing a screen or feature.

## New or changed screen: does it need existing cross-cutting state?

Before calling a screen/ViewModel done, check every piece of app-wide state that could change what
it shows, and wire it in if it applies. **Grep for each store/flag's existing observers.** If a
screen that shows the same kind of data doesn't observe it, that's the gap.

Current cross-cutting state on Android (`android/app/src/main/java/com/sattrakk/app/`):

- **`HiddenSatellitesStore`** (`data/local/`). Any screen that lists satellites or per-satellite
  data (tabs, passes, drawers, map targets) must respect hidden satellites. Observe the Flow; don't
  read it once. Use the raw-state + `combine()` projection pattern (`DashboardViewModel`,
  `MapViewModel`) so a later poll can't bring hidden data back. Shared filter:
  `domain/util/PassFilters.kt`'s `excludeHiddenSatellites`.
- **`SessionManager.sessionState`**. It's handled centrally by `SafeApiCaller` + `SatTrakkApp`, so
  a screen normally needs nothing extra.
- **Client-side copies of backend defaults/semantics** (e.g. `PassRepository.NEW_PASS_NOTIFY_DEFAULT`
  mirroring the backend's `PassSubscription` opt-in default). When a backend default changes, grep
  the Android side for its own copy of that assumption.

Why this is a standing item: it has happened twice with `HiddenSatellitesStore` alone. Dashboard
was fixed in UI round 1, and Map and its drawer on 2026-09-29. Both times a screen was built
before, or without, the dependency, and was never retrofitted. The notify-default fix
(2026-09-29) was the backend-default variant of the same miss. See `android/CLAUDE.md`: "DashboardViewModel now
observes `HiddenSatellitesStore`", "Notify default — Android followed the backend's opt-in flip
late", and "Map vs. hidden satellites".

When a new piece of cross-cutting state is added, add it to the list above in the same change.
