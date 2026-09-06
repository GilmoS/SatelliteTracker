# CLAUDE.md — SatTrakk Android

Android client (Kotlin, Jetpack Compose) for the Satellite Pass Tracker. See the repo-root
`CLAUDE.md` for the overall project/backend context — this file only covers what's specific to
the Android app. Talks only to `SatelliteTracker.API`; never calls N2YO or Microsoft Graph
directly (see repo-root CLAUDE.md, "Single Source of Truth Rules").

This file currently covers what's built as of Milestone E, Step 2.1 (networking + auth
infrastructure), Step 2.2 (Satellite/Pass/Notes repositories with Room caching), Step 2.3
(`AuthRepository`/`SettingsRepository`), Step 3.1 (`SessionManager` + `DashboardViewModel` +
`DashboardUiState` — logic only, no UI yet), the Full Pass List screen's data layer
(`PassRepository.getPassHistory`, `HistoryLoadStateEntity`/Dao, `FullPassListViewModel` +
`FullPassListUiState` — logic only, no UI yet, see below), the Settings screen's logic layer
(`HiddenSatellitesStore`, `NotificationPermissionManager`, `SettingsViewModel` +
`SettingsUiState` — logic only, no UI yet, see below), and the Pass Details Modal's logic layer
(`PassDetailsUiState`, `PassDetailsEvent`, `PassDetailsViewModel` — logic only, no UI yet, see
below). **Step 2 (the entire Android data layer) and Step 3 (the entire ViewModel/UiState layer
for Dashboard, Full Pass List, Settings, and Pass Details) are both complete.**

Also built: the navigation graph (`MainNavHost`, all 6 routes), the app-root session-state
wrapper (`SatTrakkApp`/`ReauthScreen`), the M3 theme (`ui/theme/`, extracted from the design MCP),
the Dashboard screen's real Composable content (see "Navigation, session wrapper, M3 theme, and
the Dashboard screen" below), and the Full Pass List screen + Filter Modal's real Composable
content (`FullPassListScreen.kt`, `FilterModalSheet.kt` — see "Full Pass List screen + Filter
Modal — Composable/UI" below), including one small ViewModel addition,
`FullPassListViewModel.resetFilters()`. **Dashboard and Full Pass List are the only screens with
real content; Settings, Pass Details, Map, and Sky View all still have placeholder-only
Composables** — this file will grow again once those land.

---

## Commands

```bash
# Compile
./gradlew :app:compileDebugKotlin

# Run unit tests (JVM — data/util, data/remote interceptor, etc.)
./gradlew :app:testDebugUnitTest

# Run instrumented tests (needs a connected device/emulator — e.g. ApiKeyStoreTest,
# which needs the real Android Keystore)
./gradlew :app:connectedDebugAndroidTest

# Assemble the debug APK
./gradlew :app:assembleDebug
```

No `gradlew`/`gradlew.bat` wrapper scripts are committed yet — `gradle/wrapper/gradle-wrapper.properties`
pins the intended version (Gradle 9.3.1). Run the above via a local Gradle install matching that
version, or Android Studio's Gradle sync, until the wrapper scripts are added.

**Finding a local Gradle install, if `gradle` isn't on `PATH`**: don't conclude no usable Gradle
exists just because `gradle -v`/`which gradle` comes up empty — check these before giving up,
since a full filesystem `find`/recursive search is slow and often times out:
- `$USERPROFILE/.gradle/wrapper/dists/gradle-<version>-bin/<hash>/gradle-<version>/bin/gradle.bat`
  — a version-matching distribution is very likely already cached here from a prior Android
  Studio sync (the `<hash>` segment is machine-specific, so glob for it rather than hardcoding).
- `$ANDROID_HOME` (or Android Studio's own install dir under
  `%LOCALAPPDATA%\Google\AndroidStudio*`) for a bundled Gradle/JDK.

**If the run fails with `ERROR: JAVA_HOME is set to an invalid directory`**: the `JAVA_HOME`
env var on this machine can point at a stale/nonexistent path even though a working JDK 21 is
installed elsewhere (e.g. under `C:\Program Files\Microsoft\jdk-21.0.11.10-hotspot` rather than
`C:\Program Files\Java\jdk-21`). Override `JAVA_HOME` for just that command rather than editing
the environment, e.g.:
```bash
JAVA_HOME="C:\Program Files\Microsoft\jdk-21.0.11.10-hotspot" \
  "$USERPROFILE/.gradle/wrapper/dists/gradle-9.3.1-bin/<hash>/gradle-9.3.1/bin/gradle.bat" \
  :app:testDebugUnitTest
```

---

## Package structure

```
com.sattrakk.app/
├── data/
│   ├── remote/
│   │   ├── SatTrakkApi.kt          Retrofit interface — every backend endpoint
│   │   ├── ApiKeyInterceptor.kt    OkHttp interceptor, adds X-Api-Key
│   │   ├── serializer/             Contextual kotlinx.serialization serializers
│   │   └── dto/                    Generated DTOs (never committed — see that dir's contents)
│   ├── local/
│   │   ├── ApiKeyStore.kt          EncryptedSharedPreferences wrapper
│   │   ├── HiddenSatellitesStore.kt DataStore-backed, local-only hidden-satellite ids (Settings screen)
│   │   ├── AppDatabase.kt, PassDao.kt, SatelliteDao.kt, NoteDao.kt, CacheMetadataDao.kt,
│   │   │                          HistoryLoadStateDao.kt (Full Pass List screen)
│   │   └── entity/                 Room @Entity classes (Pass, Satellite, Note, CacheMetadata,
│   │                                HistoryLoadState)
│   ├── permission/
│   │   └── NotificationPermissionManager.kt  Read-only POST_NOTIFICATIONS status wrapper (Settings screen)
│   ├── session/
│   │   └── SessionManager.kt       Global SessionState (Valid/RequiresReauth) (step 3.1)
│   ├── util/
│   │   ├── SafeApiCaller.kt        Retrofit Response<T> -> ApiResult<T> mapping, injects SessionManager (step 3.1)
│   │   └── CachedNetworkFirst.kt   Shared TTL-gated caching decision tree (step 2.2)
│   └── repository/                 SatelliteRepository, PassRepository (getPasses/getPassHistory/
│                                    etc.), NotesRepository (step 2.2), AuthRepository,
│                                    SettingsRepository (step 2.3)
├── domain/
│   ├── model/
│   │   ├── ApiResult.kt            Uniform outcome type for every repository call
│   │   ├── Satellite.kt, Pass.kt, Note.kt, PassTrack.kt, NotifyStatus.kt (step 2.2)
│   │   ├── UserSettings.kt         (step 2.3)
│   │   └── TimeWindow.kt, PassHistoryFilter.kt, PagedResult.kt (Full Pass List screen)
│   └── mapper/                     Dto <-> Entity <-> domain extension functions (step 2.2/2.3),
│                                    PassHistoryFilterMappers.kt (Full Pass List screen)
├── di/
│   ├── NetworkModule.kt            OkHttpClient, Json, Retrofit, SatTrakkApi
│   ├── DatabaseModule.kt           Room AppDatabase + DAOs
│   ├── ClockModule.kt              java.time.Clock, for testable "now" (step 3.1)
│   ├── CoroutineScopeModule.kt     @ApplicationScope CoroutineScope, for fire-and-forget work outliving a caller
│   ├── DataStoreModule.kt          Preferences DataStore singleton + HiddenSatellitesStore binding (Settings screen)
│   └── PermissionModule.kt         NotificationPermissionManager binding (Settings screen)
├── ui/
│   ├── theme/                       Color.kt, Shape.kt, Type.kt, Theme.kt — M3 tokens from the
│   │                                design MCP (see below)
│   ├── reauth/
│   │   └── ReauthScreen.kt          Dead-end screen shown when SessionManager requires reauth
│   ├── dashboard/
│   │   ├── DashboardUiState.kt      SatelliteTabState (+ nextPass, added this task) + DashboardUiState
│   │   ├── DashboardViewModel.kt    Dashboard screen logic
│   │   └── DashboardScreen.kt       Real Composable content — see below
│   ├── fullpasslist/
│   │   ├── FullPassListUiState.kt   PassListFilter + FullPassListUiState (Full Pass List screen)
│   │   ├── FullPassListViewModel.kt Full Pass List screen logic (+ resetFilters(), added this task)
│   │   ├── FullPassListScreen.kt    Real Composable content — see below
│   │   └── FilterModalSheet.kt      Filter Modal bottom sheet — see below
│   ├── settings/
│   │   ├── SettingsUiState.kt      SatelliteVisibility + SettingsUiState (Settings screen)
│   │   ├── SettingsViewModel.kt    Settings screen logic
│   │   └── SettingsScreen.kt       Placeholder only
│   ├── passdetails/
│   │   ├── PassDetailsUiState.kt   EditingNoteState + PassDetailsUiState (Pass Details Modal)
│   │   ├── PassDetailsEvent.kt     One-shot NavigateToMap event (Pass Details Modal)
│   │   ├── PassDetailsViewModel.kt Pass Details Modal logic
│   │   └── PassDetailsScreen.kt    Placeholder only (registered as a dialog destination)
│   ├── map/MapScreen.kt             Placeholder only (Milestone F)
│   └── skyview/SkyViewScreen.kt     Placeholder only (Milestone F)
├── navigation/
│   ├── SatTrakkApp.kt               App root: SatTrakkTheme + SessionManager switch (see below)
│   ├── SatTrakkNavHost.kt           SatTrakkDestination routes + MainNavHost (Scaffold + bottom nav)
│   └── NavIcons.kt                  Small original Canvas-drawn icons: nav/FAB/chevron, plus
│                                      back arrow/filter/close (added for Full Pass List)
```

---

## Generated DTOs and the OpenAPI response-schema fix

`data/remote/dto/` is generated at build time from `openapi/sattrakk-api.json` (see that dir's
own README and `app/build.gradle.kts`'s `openApiGenerate` block) — nothing in it is committed.

As of Milestone E Step 2.1, every backend controller action returned bare `IActionResult` with no
`[ProducesResponseType]` attributes, so Swashbuckle could only document `200: OK` with no content
schema for any endpoint — the generator had nothing to build response DTOs from (only the request
bodies, e.g. `CreateNoteRequest`, generated). Fixed on the backend side (not just worked around
here) by adding `[ProducesResponseType(typeof(X), ...)]` to every action across all controllers,
adding a shared `ErrorResponseDto` for the `{"error": "..."}` shape, and replacing `PatchNotify`'s
anonymous response object with a typed `NotifyStatusDto` — see the backend CLAUDE.md/git history
for that change. Regenerate the spec (`openapi/README.md`) and re-run `openApiGenerate` after any
future controller change that adds a new endpoint or changes a response shape.

## Contextual serializers — UUID and OffsetDateTime

The generator's `kotlinx_serialization` option marks every `Guid`/`DateTime` field `@Contextual`
instead of emitting serializers for them, and the models-only codegen mode this project uses
(`openApiGenerate`'s `globalProperties`) never generates supporting infrastructure files either.
`data/remote/serializer/ContextualSerializers.kt` supplies both (`UuidSerializer`,
`OffsetDateTimeSerializer`) and the `SerializersModule` that wires them into the shared `Json`
instance (`di/NetworkModule.kt`). Both are plain ISO-8601 string (de)serializers — the backend's
`DateTime` columns are all Postgres `timestamp with time zone`, so `System.Text.Json` always
emits a real UTC offset (`Z`), never a bare local-looking timestamp.

## ApiKeyStore and the auth interceptor

- **`ApiKeyStore`** (`data/local/`) is the single place in the app that touches
  `EncryptedSharedPreferences` — everything else that needs the raw API key goes through
  `getKey()`/`saveKey()`. It's tested as an instrumented test (`androidTest/`), not a JVM unit
  test, because `EncryptedSharedPreferences` needs the real Android Keystore.
  `androidx.security:security-crypto`'s `EncryptedSharedPreferences`/`MasterKey` classes are
  marked deprecated upstream (`MasterKey`: "use `javax.crypto.KeyGenerator` with AndroidKeyStore
  instead"; `EncryptedSharedPreferences`: "use `android.content.SharedPreferences` instead," i.e.
  roll your own AndroidKeyStore-backed encryption) with no maintained replacement shipped — the
  class itself is `@Suppress("DEPRECATION")`d with a comment explaining this, rather than hand-
  rolling key management. This is a known, accepted tradeoff, not something to "fix" by swapping
  libraries.
- **`ApiKeyInterceptor`** (`data/remote/`) adds `X-Api-Key` to every outgoing request when a key
  is stored, and sends the request unmodified when none is stored — deliberately unconditional,
  with no per-endpoint logic. This is safe because anonymous GETs ignore the header (the backend
  only reads it on `[Authorize]`d actions — see repo-root CLAUDE.md's "ApiKey" auth scheme
  section), and `POST /api/auth/register` is the one endpoint that must keep working with no key
  stored yet.

## ApiResult and SafeApiCaller — the uniform result contract

`domain/model/ApiResult.kt` is what every future repository method returns:

```kotlin
sealed class ApiResult<out T> {
    data class Success<T>(val data: T) : ApiResult<T>()
    data class Error(val code: Int?, val message: String) : ApiResult<Nothing>()
    object AuthRequired : ApiResult<Nothing>()
    object NetworkError : ApiResult<Nothing>()
}
```

`data/util/SafeApiCaller.kt`'s `SafeApiCaller` wraps a Retrofit call and produces this — every
repository built in steps 2.2/2.3+ must use it rather than handling
Retrofit/OkHttp/kotlinx.serialization exceptions itself, so this mapping only exists in one
place:

- HTTP 401 → `AuthRequired`, always — regardless of which endpoint returned it, **and** calls
  `SessionManager.markReauthRequired()` (step 3.1 — see below) at this exact point. This is the
  single point of truth for "the stored key is missing/invalid/inactive" across the whole app;
  every ViewModel observes `SessionManager.sessionState` to react (route to a re-registration
  flow), matching the backend's uniform 401 body for all three cases (see repo-root CLAUDE.md).
- Other non-2xx codes → `Error(code, message)`, where `message` is parsed from the response body
  if it matches the backend's `{"error": "..."}` shape, else a generic
  `"Request failed with status {code}"`. Does **not** touch `SessionManager`.
- `IOException` (thrown by the call itself, e.g. no connectivity) → `NetworkError`. Does **not**
  touch `SessionManager`.
- Success → `Success(body)`.
- Success is decided by `response.isSuccessful` alone, not "successful and body non-null" — some
  endpoints (e.g. `DELETE /api/notes/{id}`) return 200 with no content, and `SatTrakkApi` declares
  those as `Response<Void>` (body always `null` by design) rather than `Response<Unit>` (would
  make kotlinx.serialization try to decode an empty body and throw). Treating a null body as
  failure would misclassify every one of those calls.

**`SafeApiCaller` is a class (`@Singleton`, `@Inject constructor(sessionManager: SessionManager)`
with `operator fun invoke`), not a free top-level function like the original step 2.1 design** —
it needs `SessionManager`, and every repository already follows the `@Singleton`/`@Inject
constructor` DI pattern for its own dependencies, so injecting `SafeApiCaller` the same way (as a
constructor property literally named `safeApiCall`) meant every existing `safeApiCall { api.foo()
}` call site across all five repositories kept working unchanged — only the constructor
parameter and import changed. This was a deliberate choice over threading a `SessionManager`
parameter through every individual call site by hand.

## SessionManager — global re-authentication state (Step 3.1)

`data/session/SessionManager.kt`:

```kotlin
sealed interface SessionState {
    object Valid : SessionState
    object RequiresReauth : SessionState
}

@Singleton
class SessionManager @Inject constructor() {
    val sessionState: StateFlow<SessionState> // backed by a MutableStateFlow, default Valid
    fun markReauthRequired()
    fun markValid()
}
```

Session invalidation is modeled as **state, not a one-shot event stream** — per current official
Android guidance (state-driven UI over event-driven), so a future root composable can observe
`sessionState` and react correctly regardless of how many times it's (re)collected across
recomposition/process death, rather than consuming a single navigation event that could be missed
across a config change. **`SafeApiCaller` is the single writer of `RequiresReauth`**, at the exact
point a 401 is mapped to `ApiResult.AuthRequired` (see above) — no repository or ViewModel calls
`SessionManager` directly for this. `markValid()` is called after a successful re-registration,
once that flow exists in a future step (it exists on `SessionManager` now but has no caller yet).

## DashboardViewModel — dashboard screen logic (Step 3.1)

`ui/dashboard/DashboardViewModel.kt` + `DashboardUiState.kt`. This is the first ViewModel in the
app and the first thing wired to the Step 2 data layer; there is deliberately no Composable/UI
built against it yet (that's a later step) — it's covered entirely by
`DashboardViewModelTest`.

- **Generic over whatever satellites the backend returns — never hardcoded to EROS C3 /
  RUNNER 1.** `DashboardUiState.Content.tabs: List<SatelliteTabState>` is built from
  `SatelliteRepository.getSatellites()`'s actual result, one tab per satellite, with no assumption
  about count or identity. The default selected tab uses `Satellite.isDefault` (already present on
  the domain model from step 2.2) rather than always picking the first satellite in the list.
- **Per-satellite passes are loaded in parallel** (`async`/`awaitAll`) on init, since they're
  independent of each other.
- **Per-tab error handling, not whole-screen**: if `getSatellites()` itself fails, the whole
  screen is `DashboardUiState.Error`. But if satellites load fine and only one satellite's
  `getPasses()` call fails, that tab keeps `passes = emptyList()` (or its last-known list, if a
  poll/refresh fails after an earlier success) and gets `SatelliteTabState.loadError: String?`
  set, while the other tabs are unaffected — chosen over failing the whole screen because one
  satellite's endpoint having a bad moment shouldn't blank out data the user can already see for
  the others. This field is not part of the original task sketch; it was added specifically to
  support this behavior.
- **5-minute polling relies entirely on the existing 1-hour Passes TTL (step 2.2's
  `cachedNetworkFirst`) — it does NOT mean a network call every 5 minutes.** The poll loop calls
  `PassRepository.getPasses(satelliteId, forceRefresh = false)` for every loaded tab every 5
  minutes; most of those calls are served straight from the still-fresh Room cache with no network
  hit at all, and only actually reach the backend once the 1-hour TTL has elapsed. **Do not** turn
  this into an unconditional network poll later — the whole point of the TTL layer is that callers
  above it don't need to reason about freshness themselves.
- **`refresh()` (for a future pull-to-refresh) force-refreshes only the currently selected tab**,
  not every satellite — the user pulling to refresh is asking about what they're looking at.
- **`selectTab()` is a pure local state update** — it never calls a repository — but it does
  restart the countdown ticker (below) for the newly selected satellite.
- **Countdown ticker**: for the selected tab only, a `viewModelScope` coroutine recomputes
  `Duration.between(now, nextUpcomingPass.aos)` every second (`delay(1000)` loop) and writes it to
  that tab's `nextPassCountdown`. Recomputed from scratch each tick (not decremented from a
  captured value) so a poll/refresh landing mid-countdown is picked up on the very next tick, and
  it self-corrects for `delay()` drift. Only one ticker runs at a time — `selectTab()` cancels the
  previous one and starts a new one for the newly selected tab; a non-selected tab's
  `nextPassCountdown` simply stays whatever it last was (usually `null`, since it's never ticked
  until selected). "Now" comes from an injected `java.time.Clock` (`di/ClockModule.kt`, defaults
  to `Clock.systemUTC()`) rather than `OffsetDateTime.now()` directly, specifically so tests can
  substitute a `Clock` driven by `kotlinx-coroutines-test` virtual time.
  - **Countdown-reaches-zero edge case (not specified by the original task, resolved here):**
    `nextPassCountdown` always means "time until the next pass whose AOS is still in the future."
    Once a pass's AOS arrives, it no longer qualifies as "next" and the ticker automatically rolls
    over to whatever pass comes after it (or `null` if none remain) — there is no separate
    "in-progress" state surfaced through this field; a pass currently between its own AOS and LOS
    is simply not reflected by `nextPassCountdown` at all. An equally reasonable alternative would
    have been to hold at zero or expose an explicit "in progress" state until LOS — flagged here
    rather than silently decided, per the task's own instructions.

**Testing note**: `DashboardViewModelTest` deliberately does **not** use `runTest { }` at all.
`viewModelScope`'s polling/countdown coroutines run for the ViewModel's whole lifetime and are
only ever cancelled by `ViewModel.onCleared()`/`clear()`, both `protected`/`internal` in AndroidX
Lifecycle (verified against the actual resolved 2.9.4 sources) and unreachable from a plain unit
test — so those `while (isActive) { ...; delay(x) }` loops never finish on their own.
`kotlinx-coroutines-test`'s `runTest { }` runs an implicit "drain to idle" pass at the end of the
test body, and once `Dispatchers.Main` has been redirected to a `TestDispatcher` (via
`MainDispatcherRule`'s `Dispatchers.setMain(...)`), that drain ends up processing Main's queue
too — even with no `TestDispatcher` explicitly passed into `runTest(...)` — so it can never reach
idle. Confirmed twice via `jstack` thread dumps during this step's development: the test thread
pegged at ~100% CPU indefinitely inside `TestCoroutineScheduler.advanceUntilIdleOr`, reached from
`runTest`'s own internal builder. The actual fix: nothing in these test bodies is itself a suspend
call — the ViewModel's own coroutines do the suspending; reading `uiState.value` and MockK's
`coEvery`/`coVerify` are plain synchronous calls — so each `@Test` is a normal, non-suspend
function that drives `mainDispatcherRule.testDispatcher.scheduler` directly via its plain
(non-suspend) `runCurrent()`/`advanceTimeBy()` methods. There is then no `runTest` drain to ever
get stuck on. Any future ViewModel test with a long-lived polling/ticker coroutine should follow
the same pattern (no `runTest`, drive the Main `TestDispatcher`'s scheduler directly) rather than
wrapping the test body in `runTest { }`.

## Navigation graph, session wrapper, M3 theme, and the Dashboard screen (Milestone E)

Builds the app's navigation graph, the app-root session-state wrapper, the M3 theme, and — the
only screen with real content in this task — the Dashboard screen's Composables, wired to the
already-complete `DashboardViewModel`/`DashboardUiState` (Step 3.1, above). Driven by a field-by-
field audit of the design (Claude Design project "Map detail and AR improvements",
`claude.ai/design/p/fb57c4cc-1710-43cf-8246-39cc22b4dc34`, file `SatelliteTracker M3.dc.html`,
option **2a — Material 3 baseline**, not 2b/Expressive) against the actual ViewModel/Repository
code, read via the design MCP (`DesignSync`'s `get_project`/`list_files`/`get_file` against that
project's UUID — the same tool the `/design-sync` component-library workflow uses, pointed at a
regular project instead). The design is explicitly **not** a 1:1 mock of each screen's real
functionality — every element below was individually checked against real code before being wired
up, omitted, or approximated; nothing was assumed from the mockup alone.

### Routes — `SatTrakkDestination` (`navigation/SatTrakkNavHost.kt`)

| Route | Type | Args | Status |
|---|---|---|---|
| `dashboard` | `composable` | none | **Real content** (this task) |
| `map` | `composable` | none | Placeholder (Milestone F) |
| `sky_view` | `composable` | none | Placeholder (Milestone F) |
| `settings` | `composable` | none | Placeholder (pending a future UI task) |
| `full_pass_list/{satelliteId}/{satelliteName}` | `composable` | both required | Placeholder (pending a future UI task) |
| `pass_details/{passId}` | **`dialog`**, not `composable` | required | Placeholder (pending a future UI task) |

- **`PassDetails` is a `dialog(...)` destination, not `composable(...)`** — it must render as a
  modal overlay over whatever's behind it, not replace the full screen, per the Pass Details
  Modal's existing design decision (see that section above: "Set up as a `passdetails/{passId}`
  screen destination by the nav scaffolding, but functions as a modal dialog"). `dialog()` needs
  no extra setup here — `rememberNavController()` (the Compose-specific one, from
  `androidx.navigation.compose`) already registers a `DialogNavigator` alongside the
  `ComposeNavigator` internally.
- **`FullPassList` takes two required nav args, not one** — `FullPassListViewModel` reads both
  `satelliteId` and `satelliteName` via `SavedStateHandle` (see that section above), so the route
  is `full_pass_list/{satelliteId}/{satelliteName}`, not just `{satelliteId}`. `satelliteName` is
  free text (e.g. "EROS C3") and gets `Uri.encode`d when building the route
  (`SatTrakkDestination.FullPassList.buildRoute`); Navigation Compose decodes it back
  automatically when populating the destination's arguments — no manual decode needed at the read
  site.
- **Full Pass List, Settings, Pass Details, Map, and Sky View are placeholder-only in this
  task**, regardless of how much design detail exists for them (Map/Sky View especially — both
  have heavily-detailed 2a mockups, but are Milestone F/Step 7 work per the code truth map, not
  this task). `FullPassListScreen.kt` didn't exist before this task (unlike the other four, which
  already had one-line text placeholders from the Milestone E skeleton) — created here as part of
  wiring the nav graph, still placeholder-only content.

### `SatTrakkApp` / `ReauthScreen` — session-state root wrapper (`navigation/SatTrakkApp.kt`, `ui/reauth/ReauthScreen.kt`)

`SatTrakkApp` is the new composable root (`MainActivity.setContent { SatTrakkApp(sessionManager =
sessionManager) }`, with `sessionManager` field-injected into the `@AndroidEntryPoint` Activity).
It wraps `SatTrakkTheme`, collects `SessionManager.sessionState` via
`collectAsStateWithLifecycle()` (new dependency: `androidx.lifecycle:lifecycle-runtime-compose`,
added alongside the existing `lifecycle-runtime-ktx`/`lifecycle-viewmodel-ktx`), and swaps between
`MainNavHost()` (the entire nav graph) and `ReauthScreen()` — a minimal, new, not-in-the-design
dead end explaining that re-registration is required and to contact the dev team. There is no
self-service re-registration flow yet (`SessionManager.markValid()` still has no caller — see
that section above), so `ReauthScreen` deliberately offers no retry action. `SatTrakkApp`'s
`sessionManager` parameter defaults to a fresh `SessionManager()` (always `Valid` — its
constructor takes no arguments) for previews/tooling; production wiring always passes the real
Hilt singleton explicitly.

### M3 theme — `ui/theme/` (Color.kt, Shape.kt, Type.kt, Theme.kt)

Extracted from the design's **"M3 Home"** screen (2a) inline styles via the design MCP — replaces
the prior placeholder "Mission Control" palette that predated any design-file access. Dark scheme
only, matching the design (no light variant exists in it).

- **Color**: `primary`/`onPrimary`/`primaryContainer`/`onPrimaryContainer`,
  `secondaryContainer`/`onSecondaryContainer`, `background`/`onBackground`, `surface`/`onSurface`/
  `onSurfaceVariant`, `outline`/`outlineVariant`, and all three `surfaceContainer*` tonal-elevation
  tiers actually used on the Home screen (`surfaceContainerLow` — bottom nav bar,
  `surfaceContainer` — the "next pass" card, `surfaceContainerHigh` — metric cards) are real,
  sampled values. `tertiary` (the amber accent) is captured for future screens even though nothing
  in this task's scope renders it. **Not present anywhere on the Home screen and left as Compose's
  own M3 baseline defaults or a carried-over placeholder**: bare `secondary`/`onSecondary` (only
  ever seen as a "container" tone here), `tertiaryContainer`/`onTertiaryContainer`, and `error`
  (kept as the pre-design placeholder red — no in-scope screen exercises an error state). Revisit
  once a screen that actually uses one of these (Settings, Pass Filter) gets built against the
  design. One extra non-role constant, `OnSecondaryContainerVariant`, captures a dimmer tone the
  design uses for a highlighted row's secondary text/chevron that doesn't map to any named M3
  ColorScheme role.
- **Shape**: `SatTrakkShapes` — 8dp/12dp/16dp for small/medium/large, matching the design's chips,
  metric cards, and next-pass-card/list-row/FAB corner radii exactly (these happen to already be
  the stock M3 baseline values, so no real customization was needed beyond making the scale
  explicit and passing it into `MaterialTheme`, which the theme never did before this task).
- **Type**: `SatTrakkTypography` now sets `titleLarge` (22sp, corrected to **Normal/400** weight —
  the pre-design placeholder had it at SemiBold/600, which doesn't match the design's actual top
  app bar title), `titleSmall`/`labelLarge` (14sp/500), `labelMedium` (12sp/500), `labelSmall`
  (10sp/500 — deviates from the M3 stock 11sp to match the design's actual metric-card label size
  rather than forcing the nearest stock value), and `bodySmall` (12sp/400). Font family corrected
  from the placeholder's "Inter" to **Roboto** (`RobotoFontFamily = FontFamily.Default`, which
  already renders as Roboto on stock Android — no asset-bundling caveat needed for this one,
  unlike the mono family). `TelemetryTextStyle` (JetBrains Mono fallback, still `FontFamily
  .Monospace`) stays a single base style — call sites `.copy(fontSize = ...)` it for the different
  pixel sizes the design uses in different contexts (32sp hero countdown, 14-15sp metric/list
  values) rather than the type scale growing a same-family entry per size.

### Icons — `navigation/NavIcons.kt`, original glyphs, not the design's SVGs

The design's nav bar/FAB/chevron icons are inline SVG `path`/`ellipse` elements with literal `d`
attributes. Reproducing them verbatim would need SVG path-string parsing (Compose UI does ship
`androidx.compose.ui.graphics.vector.PathParser` for exactly this, but wiring it up for five
one-off icons this small wasn't judged worth the added complexity). `NavIcons.kt` instead has five
small original `Canvas`-drawn glyphs (`HomeIcon`, `PassesIcon`, `MapIcon`, `OrbitIcon` — reused for
both Sky View and the Dashboard FAB, `SettingsIcon`) plus `ChevronIcon` (pass-row disclosure
arrow), at the same 24dp/~1.9dp-stroke convention as the design, chosen to be recognizable and
mutually distinct — not pixel-accurate reproductions.

### Five-item bottom nav bar — Passes added beyond the raw design

The design's Home screen mockup shows four nav items (Home/Map/Sky View/Settings). Per this task's
explicit instructions, a fifth — **Passes** — was added deliberately, restoring the
two-entry-point plan for Full Pass List (a Dashboard-side button and a navbar entry). Driven
entirely by `NavHostController.currentBackStackEntryAsState()` in `SatTrakkBottomNavBar` — there
is no separately-tracked "selected tab" state anywhere.

Full Pass List is scoped to one satellite (`satelliteId` + `satelliteName`, both required nav
args), but the nav bar has no independent notion of "which satellite" outside of whatever the
Dashboard is currently showing. `MainNavHost` hoists a small piece of local UI state —
`selectedSatellite: Pair<String, String>?`, not owned by any ViewModel — updated via
`DashboardScreen`'s `onSelectedSatelliteChanged` callback (fired once on initial load and again on
every tab switch, sourced from `SatelliteTabState.satelliteName`, already present — no extra
lookup call). **Chosen fallback for "no satellite known yet"**: the Passes nav item is `enabled =
false` (dimmed, non-clickable) until the Dashboard reports a selection, rather than inventing a
"default satellite" concept that doesn't exist anywhere in the ViewModel layer. In practice this
window is brief — Dashboard is the start destination, so by the time a user can reach the bottom
nav bar at all, it has almost always already reported its selection.

### Dashboard-side "view full pass list" entry point — placement

No exact design element maps to this (per the code truth map). Placed as a "View all" `TextButton`
directly beside the "Upcoming passes" section header, inside `DashboardContent` — a common M3
"see all" pattern next to the section it lists, rather than e.g. next to the tabs.

### Hero-pass derivation — `SatelliteTabState.nextPass`, not a Composable-side recomputation

The design's hero card (satellite chip, countdown, AOS/LOS/MAX EL/DUR metrics) needs the actual
next `Pass` object, but `DashboardUiState` only exposed `nextPassCountdown: Duration?` before this
task. `DashboardViewModel.startCountdownTicker` already computed "the earliest pass whose AOS is
still in the future" internally every second to drive the countdown — reusing that instead of
recomputing "find nearest future pass" a second time in the Composable layer was the whole point
of the task's instruction here. `SatelliteTabState` gained a `nextPass: Pass? = null` field set
alongside `nextPassCountdown` in the same `updateCountdown` call, covered by two new assertions in
the existing `DashboardViewModelTest` (no new test methods needed — the existing countdown tests
already exercise exactly the cases that matter: initial computation and the AOS-rollover edge
case). Like `nextPassCountdown`, `nextPass` is only ever populated for the selected tab.

### Dashboard screen — REAL/PARTIAL/DECORATIVE treatment (`ui/dashboard/DashboardScreen.kt`)

Per-element treatment, following the code truth map exactly:

- **[REAL], wired directly**: per-satellite tabs (`PrimaryTabRow`, `selectTab`), AOS/LOS/MAX
  EL/DUR metric cards, the upcoming-passes list, the bottom nav bar (shared chrome — see above).
- **[PARTIAL], derived at render time, not stored anywhere**:
  - Per-row relative time ("in 47 min") — computed fresh from `Pass.aos` and `OffsetDateTime.now()`
    inside the Composable body, **not** via its own clock/ticker. `DashboardViewModel`'s countdown
    ticker already causes a `Content` recomposition every second (it emits a new state object each
    tick), so every row's relative-time string naturally recomputes on the same cadence for free.
  - The next-pass hero card and metric grid — see "Hero-pass derivation" above.
  - Satellite-name initials on each row's avatar circle — no avatar/abbreviation concept exists on
    `Satellite` anywhere in the schema, so this is a **generic** derivation (first two
    letters/digits of the satellite name, uppercased), not a hardcoded "EROS C3" → "E3" mapping —
    hardcoding specific satellite names would violate `DashboardViewModel`'s own "never hardcoded
    to EROS C3/RUNNER 1" rule for a backend that can return any satellite list. The design's own
    literal glyphs ("E3"/"R1") aren't reproduced exactly for this reason — flagged as a deliberate
    deviation. Row highlighting (primary-tinted avatar/background/chip vs. the default
    secondaryContainer/outline styling) tracks whether a row **is the same pass shown in the hero
    card** (`pass.id == nextPass?.id`), not satellite identity — re-reading the design's own row
    styling this way (rather than as a literal per-satellite color) is what makes it map onto the
    real per-tab-scoped data model at all, since every row in a tab's list already belongs to that
    same one satellite.
- **[DECORATIVE], omitted entirely** (no layout gap results from omitting them): the OS status bar
  (edge-to-edge already handles this), the notification bell + amber badge dot (no
  unread/notification concept anywhere), and the "Alert 15 min before" assist chip
  (`alertMinutes` lives on `UserSettings`/`SettingsViewModel`, which `DashboardViewModel` never
  loads — wiring it here would mean inventing a repository call this ViewModel doesn't have).
- **[DECORATIVE], kept as static non-interactive chrome** (omitting would leave a visible empty
  gap): the elapsed-ratio ring around the countdown. `DashboardUiState` only exposes a raw
  remaining `Duration`, never an elapsed *fraction* — there's no pass-start time exposed anywhere
  to compute one against — so it renders as a plain static ring outline (no arc, no fabricated
  "62%"/"elapsed" text) rather than inventing a percentage.
- **FAB target**: pure navigation (truth map: "fine as pure navigation to Map/Sky View, routes
  exist"). Wired to **Map** — its icon (an orbiting dot) reads as "track on a map," and Sky View
  stays reachable from the bottom nav bar regardless.
- **Empty/error states not specified by the design, resolved here**: no satellites at all → "No
  satellites configured." No upcoming pass for the selected tab → hero card and metric grid are
  skipped entirely in favor of a plain "No upcoming passes." message (rather than rendering a hero
  card with nothing to show). A tab's `loadError` (a real, existing field — see
  `SatelliteTabState.loadError` above) is surfaced as a small error-colored banner below the tabs,
  since it's a real per-tab signal the ViewModel already produces and nothing else in the UI would
  otherwise show it.

### Testing

No Compose UI testing convention exists in this project beyond one coarse instrumented smoke test
(`MainActivityTest`, asserting a literal string exists after the real Activity launches) — no
per-screen `*ScreenTest.kt` pattern to follow, and none was invented here per the task's own
instructions to flag this rather than skip silently or invent one unprompted. `MainActivityTest`
itself needed updating regardless of that: it asserted `onNodeWithText("Dashboard")`, which no
longer exists anywhere on screen now that Dashboard shows real ViewModel-driven content instead of
a one-line placeholder — updated to assert on the top app bar's static "SatelliteTracker" title,
present regardless of load state.

Verified in this environment: `./gradlew :app:compileDebugKotlin`, `:app:testDebugUnitTest` (all
119 existing unit tests green — two of them, in `DashboardViewModelTest`, gained extra assertions
for the new `nextPass` field rather than new test methods), and `:app:assembleDebug`, all
`BUILD SUCCESSFUL`. **Not verified**: `:app:connectedDebugAndroidTest` (`MainActivityTest`,
`ApiKeyStoreTest`) — no `adb`/connected device or emulator was available in this environment.
Nothing about this task's changes is known to affect `ApiKeyStoreTest` specifically; running the
full instrumented suite before merging is recommended regardless.

## Full Pass List screen — history pagination + merged upcoming/history list (Milestone E)

A separate destination from the Dashboard (reachable via a button from Dashboard and the bottom
navbar, not built yet — no Composable exists for this screen). One continuous, mixed-chronology
list for a single satellite at a time: already-loaded upcoming passes (existing `PassRepository
.getPasses`) combined with paginated historical passes (`PassRepository.getPassHistory`, backed by
the backend's paginated pass-history endpoint — repo-root CLAUDE.md), filterable by
Upcoming/History/All plus a time window and a minimum elevation. `FullPassListViewModel` +
`FullPassListUiState` (`ui/fullpasslist/`) cover the logic; `PassRepository.getPassHistory` +
`HistoryLoadStateEntity`/`HistoryLoadStateDao` (`data/local/`) cover the data layer. Covered by
`PassRepositoryHistoryTest` and `FullPassListViewModelTest`.

### `HistoryLoadStateEntity` vs. `CacheMetadataEntity` — load-state tracking, not a TTL

`HistoryLoadStateEntity` (`satelliteId`, `isFullyLoaded`, `lastVerifiedAtEpochMillis`) is **not**
another `CacheMetadataEntity` row and does not go through `cachedNetworkFirst`. It does not
duplicate `PassEntity`/`PassDao` either — historical and upcoming passes share the exact same
`passes` table (consistent with the backend's own single-table model), so the only new state is
"has this satellite's full history ever been paginated to its true end."

- `CacheMetadataEntity` answers "was this cached at all, and is it within its TTL" — a pure
  time-based freshness check, used identically for every TTL-gated resource (satellites, passes,
  notes).
- `HistoryLoadStateEntity` answers a different question: "is every historical pass for this
  satellite already sitting in Room, so a filtered/paginated request never needs the network at
  all." `lastVerifiedAtEpochMillis` still gates a 1h freshness window (so a fully-loaded satellite
  isn't re-verified against the backend on every screen visit), but that's layered on top of the
  `isFullyLoaded` flag, not a replacement for it — a fresh-but-`isFullyLoaded = false` state still
  always calls the network.

### The critical distinction: `isFullyLoaded` only ever comes from an UNFILTERED fetch

`PassRepository.getPassHistory`'s decision tree: if `HistoryLoadStateEntity.isFullyLoaded` is true
and its `lastVerifiedAtEpochMillis` is within the 1h freshness window, every request — for ANY
filter or page — is served entirely from Room (`PassDao.getFilteredForSatellite`, which mirrors
the backend's own filter semantics and its "query `pageSize + 1` rows to compute `hasMore`" trick,
repo-root CLAUDE.md). Otherwise the backend's paginated history endpoint is called, results are
upserted via the existing single-row `PassDao.upsert` (the same one `getPassById` uses — never
`replaceForSatellite`, which would wipe the rest of the satellite's cached passes), and:

**A network fetch is only allowed to set `isFullyLoaded = true` when the query was UNFILTERED
(`ResolvedHistoryQuery.isUnfiltered` — no `aosFrom`/`aosTo`/`maxElevationFrom` at all) AND
`hasMore` came back false.** A filtered fetch reaching `hasMore = false` means only that *that
filtered query* is exhausted — e.g. paginating all of "Last24h" to its end says nothing about
whether the other ~6 months of history are cached — and must never be read as "the whole dataset
is loaded." Being mid-pagination doesn't set it either (`hasMore` is only false on a query's last
page by definition). `PassRepositoryHistoryTest` has an explicit test for exactly this: a filtered
fetch with `hasMore = false` leaves `HistoryLoadStateDao.upsert` uncalled.

This is where it gets non-obvious enough to flag twice: **`TimeWindow`'s three relative cases
(`Last24h`/`Last48h`/`Last7Days`) always resolve a non-null `aosFrom`**, so none of them can ever
produce an unfiltered query — only `TimeWindow.Custom(from = null, to = null)` with no
`minMaxElevation` can. `Custom`'s bounds are therefore deliberately independent and nullable
(`from: OffsetDateTime?`, `to: OffsetDateTime?`), not the non-null pair a literal reading of the
original task sketch (`Custom(from, to)`) might suggest — without that, `isFullyLoaded` would be
permanently unreachable, untestable dead code, since there's no separate "All time" case. This
isn't scope creep, it's what makes the mechanism actually work; see `TimeWindow`'s and
`PassHistoryFilterMappers.kt`'s doc comments. In practice, under the current Composable-less state
of this screen, nothing yet drives a `Custom(null, null)` request — this will matter once a real
"browse all history" UI affordance exists.

### The ALL filter's merge/sort logic, and why upcoming is re-sorted rather than re-queried

`FullPassListViewModel.loadAll()` fetches upcoming (via the *existing*, unmodified
`PassRepository.getPasses` — its own TTL/Room-first caching is reused as-is, no new caching logic
was added for this screen) and the first history page in parallel (`async`/`awaitAll`, same shape
as `DashboardViewModel`'s per-tab loading). `getPasses` returns ascending-by-AOS because
`DashboardViewModel` depends on that order — changing it would break the Dashboard — so this
screen re-sorts that same result descending **locally, in memory**, with no new query, rather than
asking `getPasses` for a different order. History is already descending by AOS (the backend's
fixed sort order). The merged `passes` list is upcoming-descending first, then history-descending,
so the whole list reads newest-first end to end.

`nearestPassId` is a boundary marker between the two portions, not a generic "closest pass to now"
pick — meaningless (and left `null`) in UPCOMING-only or HISTORY-only views, since there's only
one portion to show. Chosen resolution, flagged as a deliberate edge-case call rather than the only
reasonable one: the last upcoming pass in display order (smallest AOS still `>= now`, i.e. the row
sitting just above the boundary), or — if there are no upcoming passes at all — the first
(most recent) history pass instead.

Pagination (`loadMore()`) only ever fetches the next history page and appends it to the tail of
`passes`; the upcoming portion, already fully loaded up front, is never re-fetched or disturbed —
this works identically whether the current filter is HISTORY or ALL, since in both cases the tail
of the list is always the history portion.

### Filter changes always reset and reload from scratch

Changing `filter`, `timeWindow`, or `minMaxElevation` (`setFilter`/`setTimeWindow`/
`setMinMaxElevation`) resets `historyPage` to 1 and rebuilds `passes` from scratch — it's treated
as a new query, not an incremental update. There's no free-text search on this screen (filters
only), so there's deliberately no "clear search restores scroll position" behavior to preserve —
don't add it back in; it doesn't apply here.

### Duration and pass-direction filters are NOT implemented

Only `timeWindow` (→ `aosFrom`/`aosTo`) and `minMaxElevation` (→ `maxElevationFrom`) are real,
backed filters. Any duration- or pass-direction-based filter controls visible in a shared design
mockup for this screen are **not** implemented — those fields don't exist as backend filter params
(repo-root CLAUDE.md's paginated pass history section only defines `orbitNumberFrom/To`,
`maxElevationFrom/To`, `aosFrom/To`, `losFrom/To`, and even of those, only the two mapped here are
exposed by `PassHistoryFilter`). Flag this explicitly to whoever wires the Composable UI up next.

---

## Full Pass List screen + Filter Modal — Composable/UI (Milestone E)

Replaces the placeholder from the nav-graph task with real content: `FullPassListScreen.kt`
(`ui/fullpasslist/`) and `FilterModalSheet.kt`, wired to the already-complete
`FullPassListViewModel`/`FullPassListUiState` above, plus one small ViewModel addition
(`resetFilters()`). Driven by the code truth map's Screens 2/8 (Upcoming), 3/8 (History), and 4/8
(Filter Modal) verdicts. Does **not** touch Dashboard, Settings, Pass Details, Map, or Sky View.

### `resetFilters()` and the `DEFAULT_TIME_WINDOW`/`DEFAULT_MIN_MAX_ELEVATION` constants

`FullPassListViewModel` gained `fun resetFilters()`, resetting `timeWindow` and `minMaxElevation`
(not `filter` — the Upcoming/History/All choice isn't a Filter Modal control, and "reset filters"
shouldn't silently switch the user off whichever of the three they're looking at) to two new
public companion constants, `DEFAULT_TIME_WINDOW` (`TimeWindow.Last7Days`) and
`DEFAULT_MIN_MAX_ELEVATION` (`null`) — the same values the initial `FullPassListUiState` already
used inline. Making them public, named constants (rather than leaving the defaults as inline
literals) means the Composable layer's filter-badge-count and active-filter-chip derivation (see
below) compares against the exact same "default" `FullPassListViewModel` itself uses, instead of
a second hardcoded copy that could drift. Like the existing setters, `resetFilters()` no-ops (no
reload) if already at the defaults. Covered by two new `FullPassListViewModelTest` cases.

### Single-satellite scope is final — not a gap

`FullPassListViewModel` stays permanently scoped to one `satelliteId` via `SavedStateHandle`. The
design mockup's "All / EROS C3 / RUNNER-1 / VENμS" satellite-tabs row (Screen 2/8) and the Filter
Modal's satellite multi-select chips (Screen 4/8) are both omitted entirely — there is no
multi-satellite aggregation anywhere in this ViewModel, and none was added. **If multi-satellite
browsing becomes a real requirement later, it needs a deliberate `FullPassListViewModel` redesign,
not a quick UI addition** — the current architecture (one `satelliteId` nav arg, one Room
history-load-state row per satellite, one merged list) assumes a single satellite per screen
instance throughout.

### Three-state segmented control, ALL as the real default — a deliberate deviation from the design

The design mockup shows a two-way Upcoming/History toggle. The actual control is a three-way
`SingleChoiceSegmentedButtonRow` (`PassListFilter.UPCOMING`/`.HISTORY`/`.ALL`), with **ALL as the
default on screen entry** (already `FullPassListUiState`'s initial value — no ViewModel change was
needed for this part) — a single continuous chronological list, upcoming-first then history, per
`FullPassListViewModel`'s existing merge/sort logic, with Upcoming-only and History-only as
additional filter choices rather than the primary two-way choice the mockup implies. This is a
confirmed product requirement, not derived from the design file.

### Composable-layer-only derivations — no new UiState fields beyond `resetFilters()`'s constants

Per the truth map's `[PARTIAL]` verdicts, none of these needed a new `FullPassListUiState` field:

- **Date-group headers** ("TODAY · 29 AUG", "YESTERDAY · 28 AUG", "TOMORROW · 30 AUG", or a plain
  "24 AUG" beyond that ±1-day window) — `FullPassListScreen.kt`'s private `buildGroupedItems`
  walks the already-ordered `passes` list once, grouping by calendar day
  (`Pass.aos.atZoneSameInstant(ZoneId.systemDefault()).toLocalDate()`) and inserting a header
  whenever the date changes. Never re-sorts or re-fetches — pure display grouping over data the
  ViewModel already ordered. The merged ALL list's upcoming/history boundary
  (`FullPassListUiState.nearestPassId`) is deliberately **not** surfaced as a second, separate
  divider here — the date headers already make the future-to-past transition visually obvious on
  their own, so an extra boundary marker would be redundant. `nearestPassId` stays real,
  ViewModel-computed state; this screen just doesn't have an additional use for it beyond what the
  date headers already convey.
- **Filter badge count** (the number on the Filter button, via `BadgedBox`/`Badge`) — derived by
  comparing `state.timeWindow`/`state.minMaxElevation` against `FullPassListViewModel
  .DEFAULT_TIME_WINDOW`/`.DEFAULT_MIN_MAX_ELEVATION` at render time (`buildActiveFilterChips`
  doubles as this derivation — its result list's size is the badge count).
- **"Show N passes" / list counts** — `state.passes.size` directly, nowhere else.
- **Active-filter chips** — one `InputChip` per non-default filter, each independently removable:
  tapping a chip calls the relevant setter with the **default** value (`onSetTimeWindow
  (DEFAULT_TIME_WINDOW)` / `onSetMinMaxElevation(DEFAULT_MIN_MAX_ELEVATION)`), never
  `resetFilters()`, which would clear both at once. Time-window chip labels read "Last 24h"/"Last
  48h" rather than the design's literal "Next 48h" — `TimeWindow` resolves to a **look-back**
  window (`now.minusHours(...)`, see `PassHistoryFilterMappers.resolve`), so "Next" would name the
  wrong direction; a deliberate wording correction, not a literal copy of the mockup.

### No staged/draft filter state — every control applies immediately

Per this task's confirmed decisions, no draft/staged filter state was added anywhere. Every Filter
Modal control calls its `FullPassListViewModel` setter and reloads on the spot, exactly like the
existing setters already work:

- The design's "Show N passes" commit button with a live preview count does **not** get a real
  preview. It's a plain dismiss button showing the **current** (already loading/loaded)
  `passes.size` — not a hypothetical count for a not-yet-applied filter.
- **The elevation slider is the one control that doesn't call its setter on every micro-change** —
  a local `mutableFloatStateOf` mirrors the thumb for smooth dragging, and
  `onSetMinMaxElevation` fires only in `onValueChangeFinished` (drag release). This isn't staged
  ViewModel state (nothing overrides what's actually applied in the meantime); it's the standard
  Material3 `Slider` pattern for not reloading on every intermediate drag pixel, which "applies
  immediately" was never meant to require.
- **Tapping the "Custom range" time-window chip doesn't call `onSetTimeWindow` by itself** either
  — it only reveals the from/to date fields locally (`customRangeExpanded`, a plain UI-visibility
  boolean, not a filter draft). The setter only fires once an actual date is picked in one of the
  two `DatePickerDialog`s.
- **Cancel** (truth map: "pure UI dismissal, no repository call") is fulfilled by the sheet's
  header × button (and the scrim/back gesture) alone — no separate "Cancel" button was added next
  to "Show N passes", since with no staged state to discard, a dedicated Cancel action would do
  exactly what dismissing already does.

### "All time" is an explicit chip, not a hidden empty-`Custom` state

`TimeWindow.Custom(null, null)` is the one way this filter model expresses "no time constraint"
(see `TimeWindow`'s own doc comment). The Filter Modal surfaces it as its own labeled **"All
time"** chip, tapped directly (no date picker involved) — distinct from **"Custom range"**, which
reveals two independent `DateBoundField`s ("From"/"To", each nullable on its own, each with its
own "Clear"). Picking a date converts through `LocalDate` in the *device's* local zone, not UTC —
`DatePickerDialog`'s `selectedDateMillis` is UTC-midnight internally, but the calendar day it
visually shows is read as the date the user means in their own timezone, then converted to an
`OffsetDateTime` via `date.atStartOfDay(ZoneId.systemDefault())`. **"To" is treated as through the
end of that day** (the next day's start, exclusive) rather than that day's own midnight — a
judgment call flagged here rather than silently decided, since "up to and including this day"
reads as the more useful meaning for a history filter.

### Decorative omissions — omitted entirely, not rendered disabled

Per the truth map and this task's explicit instructions: the search icon and overflow menu (no
backing action for either); the satellite multi-select chips (see single-satellite scope above);
and the Filter Modal's duration/pass-direction/sunlit-only/horizon-mask controls — none of these
have any backend param whatsoever, so none are rendered even as disabled chrome, unlike a
decorative element that might warrant a disabled state purely for layout reasons.

### Pagination

Infinite-scroll, not a "load more" tap target: a `LaunchedEffect` watches
`listState.layoutInfo.visibleItemsInfo.lastOrNull()?.index` via `snapshotFlow`, and calls
`viewModel.loadMore()` once the last visible row is within a few items of the end of the currently
loaded (grouped) list. `loadMore()` itself already no-ops for `UPCOMING` and while a load is in
flight, so this fires freely without duplicating that guard in the Composable.

### Navigation

`SatTrakkNavHost`'s `FullPassListScreen` composable call no longer passes `satelliteId`/
`satelliteName` explicitly — `hiltViewModel()` gives `FullPassListViewModel` its own
`SavedStateHandle` from the same backstack entry, so the ViewModel already carries both
(`FullPassListUiState.satelliteId`/`.satelliteName`) without threading them through a second time.
The back arrow calls `navController.popBackStack()`; row taps navigate to
`SatTrakkDestination.PassDetails.buildRoute(passId)`, the same dialog destination Dashboard's row
taps already use.

### Icons and formatting — small, local, not shared with Dashboard

`navigation/NavIcons.kt` gained `BackArrowIcon`, `FilterIcon` (funnel), and `CloseIcon` (×) —
small additions to the same shared Canvas-drawn icon set from the nav-graph task, since none of
the existing five fit. `FullPassListScreen.kt`'s time/duration/relative-time formatting helpers
are a small **local duplicate** of `DashboardScreen.kt`'s equivalents, not factored into a shared
file — this task's scope explicitly excludes touching Dashboard, and extracting a shared
formatting util would mean editing it.

---

## Settings screen — hidden satellites, permission status, alert-minute preferences (Milestone E)

A separate destination from Dashboard and Full Pass List (no Composable exists for this screen
yet — the screen is designed separately and will be wired to this logic layer in a later step).
Covers three independent concerns: which satellites are hidden from the Dashboard (purely local),
notification permission status (read-only), and the tester's alert-minute/push preferences
(backend-synced). `HiddenSatellitesStore` (`data/local/`), `NotificationPermissionManager`
(`data/permission/`), and `SettingsViewModel` + `SettingsUiState` (`ui/settings/`) cover this.
Covered by `HiddenSatellitesStoreTest`, `NotificationPermissionManagerTest`, and
`SettingsViewModelTest`.

### `HiddenSatellitesStore` — DataStore-backed, local-only, deliberately not synced

Which satellites are hidden from the Dashboard is a purely visual/local preference — it does
**not** sync to the backend, does **not** touch `UserSettings`/`SettingsRepository`, and has no
functional significance beyond what the Dashboard chooses to fetch/show. Backed by Preferences
DataStore (`androidx.datastore:datastore-preferences`), not Room — a single `stringSetPreferencesKey`
is a simple string-set preference, not structured/relational data that would benefit from a table.
`di/DataStoreModule.kt` provides one process-lifetime `DataStore<Preferences>` singleton (via the
standard `by preferencesDataStore(name = ...)` `Context` extension, one file backing every local
preference key added here or later) and binds `HiddenSatellitesStore` to its
`DataStoreHiddenSatellitesStore` implementation, following the same `object` module / `@Provides`
style every other DI module in this app uses (no `@Binds` abstract-class module exists here, so
this doesn't introduce that pattern for the first time). This survives app restarts (unlike
in-memory ViewModel state) but is device-local only: reinstalling the app or switching devices
resets it to "nothing hidden" — an accepted, deliberate tradeoff, not a bug to fix.

**DataStore enforces a single live instance per backing file, within a process** — opening a
second `DataStore` (even a distinct instance) against the same file while the first is still open
throws (`IllegalStateException` from `OkioStorage`), discovered when
`HiddenSatellitesStoreTest` originally tried to open a second instance to verify persistence
"survives a restart." This is a non-issue for the real app (`DataStoreModule` provides exactly one
Hilt singleton, so only one instance ever exists at a time), so nothing was worked around — the
test was adjusted to not open a second concurrent instance instead. Keep this in mind if a future
DataStore-backed store's test tries the same "reopen and re-read" pattern.

### `NotificationPermissionManager` — read-only POST_NOTIFICATIONS status

ViewModels must not touch `Context`/`Activity` directly (testability, lifecycle-safety) — this is
the single place permission state is read. It only **reports** status; it does **not** itself
trigger the system permission dialog — that has to happen from an Activity/Composable in a future
UI step (e.g. via `rememberLauncherForActivityResult`).

- Below API 33 (`TIRAMISU`), `POST_NOTIFICATIONS` doesn't exist as a runtime permission (granted
  at install time), so `isGranted()` unconditionally returns `true` and `shouldShowRationale()`
  unconditionally returns `false` on those devices, without calling into
  `ContextCompat`/`ActivityCompat` at all.
- **`shouldShowRationale` takes an `Activity` parameter, not the no-arg signature the original
  task sketch listed** — flagged explicitly rather than silently decided, since the literal sketch
  can't actually be implemented correctly. The only platform API for this
  (`ActivityCompat.shouldShowRequestPermissionRationale`) is defined on `Activity`, with no
  `Context`-only overload available down to this app's `minSdk` 29 —
  `PackageManager`'s own `Context`-based `shouldShowRequestPermissionRationale` wasn't added until
  API 34, which would leave API 33 (where `POST_NOTIFICATIONS` first exists) with no way to ask at
  all. Rather than holding an `Activity` reference in the `@Singleton` manager (a leak risk) or
  unsafely casting the injected Application `Context` to `Activity` (would crash — an Application
  is never an `Activity`), the caller supplies its own `Activity` at call time (sourced from the
  future UI layer, e.g. `LocalContext.current as Activity` in a Composable), and it is never
  stored. `isGranted()` stays `Context`-only via `@ApplicationContext` injection, since
  `ContextCompat.checkSelfPermission` doesn't have this problem.
- `SettingsViewModel.refreshPermissionStatus(activity)` mirrors this — it takes an `Activity`
  parameter it forwards straight through and never retains, meant to be called from the UI layer's
  onResume-equivalent lifecycle hook once that UI exists (permission state can change externally,
  e.g. the user grants it from system settings while the app is backgrounded). `SettingsViewModel`
  itself never holds an `Activity` reference.
- Testability note: `AndroidNotificationPermissionManager` exposes an `internal var
  sdkIntOverrideForTests: Int?` so `NotificationPermissionManagerTest` can exercise both the
  below-33 and 33+ branches without a reflection hack on the JVM-unit-test environment's
  `Build.VERSION.SDK_INT` (which is `0` there and not realistically fakeable via reflection — it's
  a `static final` field). This is a plain mutable property, not a constructor parameter with a
  default, specifically because Dagger/Hilt does not evaluate Kotlin default parameter values for
  `@Inject` constructors — a default-valued constructor param would force Dagger to look for a
  binding for that param's type and fail the build.

### `SettingsUiState.sendPushEnabled` is a computed property, not a stored field

The original task sketch listed `sendPushEnabled: Boolean` inline among `SettingsUiState`'s other
fields with the comment "derived: `alertMinutes.isNotEmpty()`". It's implemented as a Kotlin
computed property (`val sendPushEnabled get() = alertMinutes.isNotEmpty()`) rather than a second
stored constructor field, so it can never drift out of sync with `alertMinutes` via a `copy()` call
that updates one but not the other — a class of bug a stored field would allow. This preserves the
exact same "derived" meaning the sketch specified; only the mechanism differs.

### `sendPush` is a UI-level concept over the existing `alertMinutes` semantics — not a new backend field

Turning push off (`SettingsViewModel.setSendPushEnabled(false)`) calls
`SettingsRepository.updateAlertMinutes(emptyList())`, which the backend already treats as "no
alerts" (repo-root CLAUDE.md — this was established when `UserSettings`/`/api/settings/me` was
designed, not new behavior introduced here). There is no separate `sendPushEnabled` flag persisted
anywhere, client or server — it's purely `alertMinutes.isNotEmpty()`.

### `lastNonEmptyAlertMinutes` is in-memory only — explicitly not persisted

`SettingsUiState.lastNonEmptyAlertMinutes` lets `setSendPushEnabled(true)` restore whatever
alert-minute selection was in effect before the tester last turned push off, without asking them to
re-pick it. It is **never** written to `SettingsRepository`, `HiddenSatellitesStore`, or any other
persistence layer, and resets to empty on process death — turning push off, killing the app, and
reopening it loses the "remembered" selection. This is an accepted tradeoff, not a bug: **do not**
"fix" it into a persisted field later without deliberate discussion, since persisting it would mean
inventing new backend state (or an ambiguous local/server split) for what is currently a pure,
harmless UX nicety.

- **`setSendPushEnabled(true)` with no `lastNonEmptyAlertMinutes` to restore** (e.g. a fresh app
  start where the tester hasn't toggled push off-then-on again this session) is a genuine edge case
  the original task flagged as having two defensible resolutions. Chosen here: `SettingsUiState
  .needsAlertMinutesSelection` is set to `true`, `alertMinutes` is left untouched, and **no backend
  call is made** — silently picking default alert minutes on the tester's behalf was rejected as
  the wrong call, since there's no principled default to guess. The future UI is expected to prompt
  the tester to pick at least one alert minute when this flag is set. Cleared by the next
  successful `updateAlertMinutes` call (including one driven by `setSendPushEnabled` itself).

### `addSatellite()`/`removeSatellite()` are explicit stubs, not implemented

No backend support exists for tester-driven satellite catalog management yet (repo-root
CLAUDE.md's MVP scope already treats satellite search/add as deferred). Both methods call **no**
repository — they only set `SettingsUiState.stubMessage` to a fixed string
(`"Adding satellites isn't available yet"`), consumed by `consumeStubMessage()`. This is a
dedicated field rather than reusing `error`, so a future UI can render it as an informational
snackbar rather than an error state, and so a future implementer doesn't mistake silence here for
"nothing to do."

### No logout

Explicitly out of scope for this screen — `SessionManager.markValid()` remains uncalled (it exists
for a future re-registration flow, per step 3.1's original design) and `SettingsViewModel` has no
action that touches `SessionManager` at all. Session invalidation stays triggered only by the
backend's own 401 responses, exactly as step 3.1 established.

### Initial load: `combine()` over satellites + hidden ids, not a one-shot merge

`SettingsViewModel` holds the backend-fetched satellite catalog in a private `loadedSatellites:
MutableStateFlow<List<Satellite>>`, set once after a successful `SatelliteRepository.getSatellites()`
call on `init`, and combines it with `HiddenSatellitesStore.hiddenSatelliteIds` (a continuously-
collected `Flow`) via `kotlinx.coroutines.flow.combine` to build `SettingsUiState.satellites`. This
means a `toggleSatelliteVisibility` call's effect on `uiState.satellites` flows through the same
`combine` collector that seeded the initial value, rather than being patched into `uiState`
directly by `toggleSatelliteVisibility` itself — one code path, not two, for keeping the visibility
list in sync with the store. `alertMinutes`/`UserSettings` and `satellites`/`Satellite` are fetched
in parallel (`async`/`awaitAll`) on `init`, matching `DashboardViewModel`'s and
`FullPassListViewModel`'s existing per-source-parallel-load shape; a failure on one side doesn't
blank the other, and `error` describes only the side that failed.

---

## Pass Details Modal — pass detail + notes editing (Milestone E, Step 3 complete)

Set up as a `passdetails/{passId}` screen destination by the nav scaffolding, but functions as a
modal dialog over the Dashboard/Full Pass List, not a full-screen navigation target — no
Composable exists for this yet (designed separately). Builds entirely on the existing
`PassRepository` (`getPassById`, `setNotify`) and `NotesRepository` (`getNotes`/`createNote`/
`updateNote`/`deleteNote`) — no new repository methods, no backend changes.
`PassDetailsUiState`/`PassDetailsEvent`/`PassDetailsViewModel` (`ui/passdetails/`) cover this.
Covered by `PassDetailsViewModelTest`. **This closes out Step 3's ViewModel/UiState layer in
full** — Dashboard, Full Pass List, Settings, and Pass Details are now all done at the
ViewModel/UiState level; the remaining work is Composable/UI wiring for all four, not more
ViewModel work.

### Notes editing is dialog-based, not inline — `EditingNoteState`

Per a confirmed design decision, note creation/editing happens through a dialog, not inline in the
notes list. `PassDetailsUiState.notes` is always plain, read-only/display-only data regardless of
dialog state; `editingNote: EditingNoteState?` is the only thing that drives whether the dialog is
showing and whether it's create-mode (`EditingNoteState.NewNote`) or edit-mode
(`EditingNoteState.ExistingNote(noteId, currentContent)`). `openNewNoteDialog()`/
`openEditNoteDialog(noteId)`/`closeNoteDialog()` only ever touch this one field — none of them
call a repository. `closeNoteDialog()` in particular discards any in-progress edit with no draft
persistence, by design (matches `SettingsViewModel`'s stubs in spirit: local-only UI state, not
backed by anything durable).

### Full-state error on `getPassById` failure vs. partial content on `getNotes` failure

Pass and notes are loaded in parallel on `init` (same `async`/`awaitAll` shape as
`DashboardViewModel`'s per-tab loading and `FullPassListViewModel.loadAll`), but the two failures
are handled asymmetrically, deliberately:

- **`getPassById` fails → the entire `PassDetailsUiState` becomes an error state**: `pass` stays
  `null`, `error` is set, and `notes` is left empty even if `getNotes` succeeded in parallel — its
  result is discarded outright. Rationale: without the pass itself (AOS/LOS/elevation/notify —
  the primary content this whole modal exists to show), there's not enough left to justify
  rendering anything.
- **`getPassById` succeeds but `getNotes` fails → partial content**: `pass` is shown normally,
  `notes` is an empty list, and `error` is set to describe the notes failure. Rationale: notes are
  secondary/supplementary content — losing them for one load shouldn't hide the primary content
  the user actually opened this modal to see.

This is the opposite asymmetry from `DashboardViewModel`'s per-tab philosophy (there, *every*
tab's own failure is isolated and never blanks the *other* tabs) — here there are only two, and
one is strictly primary over the other, which is why one failure mode blanks everything and the
other doesn't.

### No optimistic updates — every mutation waits for repository confirmation

`toggleNotify()`, `saveNote()`, and `deleteNote()` all update `PassDetailsUiState` only after their
repository call returns `ApiResult.Success`, never before:

- `toggleNotify()` calls `PassRepository.setNotify(passId, !currentNotify)` and only flips
  `pass.notify` once the response's `NotifyStatus.notify` comes back — on failure, `pass` is left
  completely unchanged (no flip-then-revert flicker).
- `saveNote(content)` calls `NotesRepository.createNote`/`updateNote` (chosen by whichever
  `EditingNoteState` is currently active) and only closes the dialog and patches `notes` once the
  call succeeds. **On failure the dialog stays open and `editingNote` is left untouched** — the
  user's typed content is never discarded on a failed save, so they can retry without retyping.
  Success patches `notes` directly from the repository call's own returned `Note` (both
  `createNote`/`updateNote` already return the saved `Note` — see `NotesRepository`) rather than
  re-fetching via `getNotes`.
- `deleteNote(noteId)` only removes the note from `notes` once `NotesRepository.deleteNote`
  succeeds; on failure the note stays in the list, since the deletion didn't actually happen.

This is consistent with `NotesRepository`'s own step-2.2 no-offline-writes design (see "Notes'
asymmetry" below) — a general `error` message is considered sufficient for every mutation failure
here, including `NetworkError`; there's no dedicated "requires connection" UI state.

### `PassDetailsEvent` — the app's first Channel-based one-shot event stream

"Show on map" (`showOnMap()`) is a pure one-shot navigation signal — it emits
`PassDetailsEvent.NavigateToMap(passId)` via a `Channel<PassDetailsEvent>`/`receiveAsFlow()` and
makes no `PassDetailsUiState` change at all. This follows the state-vs-event distinction
`SessionManager` established in step 3.1 (session invalidity is *state* because it must survive
recomposition/process death; navigation here is genuinely one-time, the official Android
guidance's own carve-out for staying event-based) — but no prior event-channel convention existed
in the codebase to match, since every earlier ViewModel used only `StateFlow`. This establishes
that pattern for future screens. **The Map screen doesn't exist yet (step 6), so this event
currently has no listener — that's expected, not a gap;** it's built ready for that future wiring.

---

## Room — what's cached and why

As of step 2.2, Room caches `Pass`, `Satellite`, and `Note` — all three follow the same
TTL-gated, network-first, stale-on-`NetworkError`-only strategy (see below). No other endpoint
gets local caching; the backend's own real-time endpoints are explicitly non-cached-in-DB by
design (repo-root CLAUDE.md, "Real-time endpoints ... do NOT hit DB"), and settings/auth don't use
Room at all — see "Auth-flow repositories" below for what step 2.3 built instead.
`PassEntity`/`SatelliteEntity`/`NoteEntity` store UUID and
timestamp fields as `String`/`Long` (epoch millis) rather than `java.util.UUID`/
`java.time.OffsetDateTime`, so the entities need no Room `TypeConverter`s.

### The TTL-gated caching strategy — apply this to any future cached repository too

Every cached list read (`SatelliteRepository.getSatellites`, `PassRepository.getPasses`,
`NotesRepository.getNotes`) follows the exact same decision tree, implemented once in
`data/util/CachedNetworkFirst.kt`'s `cachedNetworkFirst()` rather than re-implemented per
repository:

1. Look up the `CacheMetadataEntity` row for that resource's cache key (see below). Its
   **presence**, not whether the cached rows list happens to be non-empty, is what "is there any
   cache at all" means — a satellite with zero upcoming passes, or a pass with zero notes, is a
   legitimate *empty but cached* result, distinct from "never fetched."
2. If a metadata row exists AND `now - lastFetchedAt < TTL` → return the Room rows directly, no
   network call at all.
3. Otherwise (missing or stale) → call the network:
   - Success → overwrite the Room rows for that key, upsert the metadata row's timestamp, return
     the fresh data.
   - Failure, specifically `ApiResult.NetworkError` (no connectivity) → fall back to whatever Room
     has for that key if the metadata row exists (even if stale); if the metadata row is absent,
     propagate `NetworkError` rather than silently returning an empty list.
   - Failure, any other `ApiResult` case (`AuthRequired`, `Error`) → propagate it as-is, never
     fall back to stale cache — a stale-but-wrong-credentials or stale-but-500 situation must
     surface the real error, not hide behind old data.
4. `forceRefresh = true` (for a future pull-to-refresh) skips step 2 entirely and always goes to
   step 3, with the same success/failure handling.

**Cache keys** are per-resource strings, not a column on the cached entity itself, because fetch
granularity differs per resource: `"satellites"` (one global key), `"passes:{satelliteId}"`,
`"notes:{passId}"`. This is why `CacheMetadataEntity(cacheKey, lastFetchedAtEpochMillis)` is a
small standalone table rather than a `lastFetchedAt` column on `PassEntity`/`SatelliteEntity`/
`NoteEntity` — a column there would need one row's timestamp to represent a whole collection's
fetch time, which doesn't fit when the collection can legitimately be empty.

**TTLs** (chosen to match the backend's own `IMemoryCache` TTLs where one exists — there's no
freshness benefit to the client polling faster than the backend itself refreshes its data):

| Resource                    | TTL      | Reason                                                |
|------------------------------|----------|--------------------------------------------------------|
| Passes list (per satellite)  | 1 hour   | Matches backend `/passes` cache TTL                    |
| Satellites list               | 24 hours | No backend cache TTL for this endpoint (changes rarely); client-side-only choice |
| Notes (per pass)              | 1 hour   | Client-side choice; no equivalent backend cache exists for `/passes/{passId}/notes`, picked to match Passes' cadence |

`PassRepository.getPassTrack` is deliberately **not** cached in Room — the backend already caches
`GET /api/passes/{id}/track` server-side for 1 hour, keyed by `passId` alone (repo-root
CLAUDE.md's caching table); a client-side cache on top would add no value. It's a straight
`safeApiCall` passthrough.

### Notes' asymmetry: cached reads, uncached writes

Notes are user-editable, not purely server-computed like Pass/Satellite, so `NotesRepository`'s
three write methods (`createNote`, `updateNote`, `deleteNote`) deliberately do **not** follow the
caching strategy above:

- They call the network directly via `safeApiCall`, with no Room read involved.
- **No offline support, and nothing is queued** — a write with no connectivity returns
  `NetworkError` like any other failure; the (future) UI is expected to surface that as "requires
  connection," not silently retry later. This is a deliberate scope decision, not a gap to close.
- On any non-`Success` result (including `NetworkError`), the local notes cache is left
  completely untouched.

### Immediate local cache update after a successful mutation

Two write paths bypass the TTL window on purpose, so a tester's own action shows up immediately
instead of up to an hour later:

- `PassRepository.setNotify` — on a successful `PATCH /api/passes/{id}/notify`, updates the
  cached `PassEntity.notify` column for that pass id directly (`PassDao.updateNotify`), without
  waiting for the next TTL-driven `getPasses` refresh. On any non-`Success` result, the cache is
  left untouched.
- `NotesRepository.createNote` / `updateNote` / `deleteNote` — on success, insert/replace
  (`NoteDao.insert`, REPLACE-on-conflict-by-id doubles as upsert) or remove
  (`NoteDao.deleteById`) the affected row in the local cache immediately.

Any future repository that adds its own mutating endpoint should follow this same pattern
(mutate → on success, patch the one affected cache row directly) rather than inventing a new one.

### `notify` is local-only state, not on `PassDto`

`PassDto` (the list/detail response shape) has no `notify` field at all — effective per-tester
notify status is sparse opt-out state living server-side in `PassSubscription` (repo-root
CLAUDE.md), not on `Pass`. `PassEntity.notify` and domain `Pass.notify` exist purely as
client-cached state: `PassRepository.getPasses`' network-success path merges in whatever `notify`
value is already cached locally for each pass id (defaulting to `true`, the backend's own sparse
default, only for a pass id seen for the first time) before writing the refreshed rows — without
this merge, a tester's own `setNotify` toggle would be silently reverted by the very next
TTL-driven or force-refreshed fetch.

### `PassRepository.getPassById` — point lookup, deliberately outside the TTL/CacheMetadata system

`getPassById(passId)` exists for the cold-deep-link case: the app opened fresh from a push
notification (or any other path) where this specific pass was never loaded via `getPasses`, so
it isn't in Room yet. It follows a Room-first/network-fallback shape like the list reads, but is
**not** wired into `cachedNetworkFirst`/`CacheMetadataEntity` at all:

1. `PassDao.getById(passId)` — if found, return it mapped to domain immediately. No network call.
2. If not found, `GET /api/passes/{id}` via `safeApiCall`, mapped to domain with `notify = true`
   (no prior local value to preserve — same default-for-new-pass rule `getPasses`' merge already
   uses).
3. On success: upsert the single row into Room via the new `PassDao.upsert` (single-row
   insert-or-replace — **not** `replaceForSatellite`, which deletes and replaces every row for a
   satellite and would wipe out the rest of that satellite's already-cached passes for a fetch
   that only concerns one pass). The satellite's passes-list `CacheMetadataEntity` row is never
   read or written by this path — this is a point lookup, not a refresh of that cached collection,
   and touching its timestamp would make a subsequent `getPasses()` call wrongly believe the full
   list was just re-fetched when it wasn't.
4. On any failure (`Error`, `AuthRequired`, `NetworkError`), propagate it as-is — there's nothing
   to fall back to for a passId Room has never seen.

**Fire-and-forget background list refresh**: on a successful single-pass fetch, `getPassById`
also triggers `getPasses(satelliteId, forceRefresh = false)` for the pass's own satellite, without
awaiting it and without letting its outcome affect what `getPassById` returns — a "since we're
here" convenience so the Dashboard's list is more likely to already include this pass by the time
the user navigates back to it, not a correctness requirement. It's launched on a new
process-lifetime `@ApplicationScope` `CoroutineScope` (`di/CoroutineScopeModule.kt`,
`SupervisorJob() + Dispatchers.IO`) rather than `viewModelScope`, because the caller of
`getPassById` (e.g. a ViewModel scoped to a pass-details dialog destination) may be cleared before
the background refresh finishes — `viewModelScope` would cancel it mid-flight. No DI concept for
this existed before this method; add future fire-and-forget work to the same scope rather than
inventing another one.

## Auth-flow repositories — AuthRepository and SettingsRepository (Step 2.3)

Closes out Step 2 (the Android data layer) in full. Both repositories are direct `safeApiCall`
passthroughs with **no Room caching**, unlike Pass/Satellite/Notes — this is an explicit decision,
not a gap: settings/auth data is per-tester, low-volume, and always needs a live round trip
(registration especially). If local caching becomes necessary later, it should follow the existing
TTL-gated pattern in `data/util/CachedNetworkFirst.kt` (see above) rather than inventing a new one.

- **`AuthRepository.register(email, displayName)`** calls `POST /api/auth/register` and, on
  success, calls the existing `ApiKeyStore.saveKey(...)` (step 2.1) with the raw key from
  `RegisterResponse.apiKey` immediately — **this repository is the single place in the app that
  ever handles the raw API key**, since it's returned by the backend exactly once, at this exact
  moment (repo-root CLAUDE.md's beta allowlist section). The raw key is never returned up to a
  ViewModel/UI layer; storage happens at the repository boundary. On any non-`Success` result
  (`403` not allowlisted, `409` already registered, `NetworkError`, ...), `saveKey` is never
  called — covered explicitly by `AuthRepositoryTest`, since a leaked key on a failed registration
  would be a serious regression.
- **`SettingsRepository`** wraps the three `/api/settings/me*` endpoints:
  - `getSettings()` — `GET /api/settings/me`, mapped to domain `UserSettings`. Always succeeds for
    an authenticated tester; a tester who's never written to either field gets the backend's
    computed default (empty `alertMinutes`, null `fcmToken`), never a `404`, so there's no
    "not found yet" branch on the client.
  - `updateAlertMinutes(minutes)` — `PUT /api/settings/me`, returns the updated `UserSettings`.
  - `updateFcmToken(token)` — `PUT /api/settings/me/fcm-token`. Returns the updated
    `UserSettings` (not `Unit`) because that endpoint's real response shape, per the regenerated
    OpenAPI spec, is the same `UserSettingsDto` the other two return.
- **`domain/model/UserSettings.kt`** (`alertMinutes: List<Int>`, `fcmToken: String?`) and its
  mapper (`domain/mapper/UserSettingsMappers.kt`) follow the same convention as every other
  DTO/domain pair: `alertMinutes` is `requireNotNull`-mapped (the backend always returns a list,
  even empty), while `fcmToken` stays nullable since null is a legitimate, expected value, not a
  contract violation.

## Base URL configuration

`BuildConfig.API_BASE_URL` (`app/build.gradle.kts` `defaultConfig`) defaults to
`http://10.0.2.2:5076/` — `10.0.2.2` is the standard Android emulator alias for the host
machine's `localhost`, and `5076` matches the backend's dev HTTP profile
(`backend/src/SatelliteTracker.API/Properties/launchSettings.json`). Override per build type (or
introduce a real staging URL) as those needs arise; there's no staging environment yet.

That default is plain HTTP, and minSdk 29 blocks cleartext traffic app-wide by default — without
an exception every request against it would fail with "CLEARTEXT communication not permitted."
`app/src/debug/res/xml/network_security_config_debug.xml` + `app/src/debug/AndroidManifest.xml`
permit cleartext to `10.0.2.2`/`localhost` **for debug builds only**; release carries no such
override and stays HTTPS-only by default. If the dev base URL ever moves to HTTPS (e.g. a real
staging host), this exception can be dropped.

## Dependency version pin — kotlinx.serialization

`kotlinxSerializationJson` is pinned to `1.8.1` in `gradle/libs.versions.toml`, not the newest
release (1.11.0) — 1.11.0's own `kotlin-stdlib` dependency resolves to 2.3.20, which the
project's Kotlin compiler (2.1.20, see that file's AGP/Kotlin pin comment) can't read, and fails
`compileDebugKotlin` with an internal compiler error ("Module was compiled with an incompatible
version of Kotlin"). 1.8.1 is what `retrofit2:converter-kotlinx-serialization:3.0.0` itself
requests, which stays on a 2.1.x-compatible stdlib. Bump this together with the `kotlin` version,
not independently.
