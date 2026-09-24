# CLAUDE.md — SatTrakk Android

Android client (Kotlin, Jetpack Compose) for the Satellite Pass Tracker. See the repo-root
`CLAUDE.md` for the overall project/backend context — this file only covers what's specific to
the Android app. Talks only to `SatelliteTracker.API`; never calls N2YO or Microsoft Graph
directly (see repo-root CLAUDE.md, "Single Source of Truth Rules").

This file currently covers what's built as of Milestone E, Step 2.1 (networking + auth
infrastructure), Step 2.2 (Satellite/Pass/Notes repositories with Room caching), Step 2.3
(`AuthRepository`/`SettingsRepository`), Step 3.1 (`SessionManager` + `DashboardViewModel` +
`DashboardUiState`), the Full Pass List screen's data layer (`PassRepository.getPassHistory`,
`HistoryLoadStateEntity`/Dao, `FullPassListViewModel` + `FullPassListUiState`), the Settings
screen's logic layer (`HiddenSatellitesStore`, `NotificationPermissionManager`,
`SettingsViewModel` + `SettingsUiState`), and the Pass Details Modal's logic layer
(`PassDetailsUiState`, `PassDetailsEvent`, `PassDetailsViewModel`, see below). **Step 2 (the
entire Android data layer) and Step 3 (the entire ViewModel/UiState layer for Dashboard, Full Pass
List, Settings, and Pass Details) are both complete.**

Also built: the navigation graph (`MainNavHost`, all 6 routes), the app-root session-state
wrapper (`SatTrakkApp`), the M3 theme (`ui/theme/`, extracted from the design MCP), the Dashboard
screen's real Composable content (see "Navigation, session wrapper, M3 theme, and the Dashboard
screen" below), the Full Pass List screen + Filter Modal's real Composable content
(`FullPassListScreen.kt`, `FilterModalSheet.kt` — see "Full Pass List screen + Filter Modal —
Composable/UI" below), including one small ViewModel addition,
`FullPassListViewModel.resetFilters()`, the Settings screen's real Composable content
(`SettingsScreen.kt` — see "Settings screen — Composable/UI" below), including one small
`SettingsUiState`/`SettingsViewModel` addition, `SatelliteVisibility.noradId`, the beta
program's Tester Entry screen (`TesterEntryScreen.kt` + `TesterEntryViewModel` — see "Tester Entry
screen" below), which replaced the earlier placeholder `ReauthScreen` outright (deleted, not kept
alongside it) as `SatTrakkApp`'s `SessionState.RequiresReauth` content, and the Pass Details
Modal's real Composable content (`PassDetailsScreen.kt` — see "Pass Details Modal — Composable/UI"
below), including two small `PassDetailsUiState`/`PassDetailsViewModel` additions
(`satelliteName`/`satelliteNoradId` resolution and the `exportToCalendar()`/`stubMessage` stub).
**This completes Step 3's full screen set** — Dashboard, Full Pass List, Settings, and Pass
Details all now have real Composable content. **Map now has real content too** (MapLibre Compose,
Milestone F — see "Map screen — MapLibre Compose UI + navigation" near the end of this file);
**Sky View remains placeholder-only**.

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
│   │   ├── FcmTokenStore.kt        DataStore-backed pending FCM token slot (Step 5)
│   │   ├── NotificationPromptStore.kt DataStore flag: Dashboard's one-time permission ask done (Step 5)
│   │   ├── AppDatabase.kt, PassDao.kt, SatelliteDao.kt, NoteDao.kt, CacheMetadataDao.kt,
│   │   │                          HistoryLoadStateDao.kt (Full Pass List screen)
│   │   └── entity/                 Room @Entity classes (Pass, Satellite, Note, CacheMetadata,
│   │                                HistoryLoadState)
│   ├── permission/
│   │   └── NotificationPermissionManager.kt  Read-only POST_NOTIFICATIONS status wrapper (Settings screen)
│   ├── push/                       FCM (Step 5) — see "FCM push notifications" below
│   │   ├── SatTrakkMessagingService.kt  onNewToken -> pending slot; foreground notification builder
│   │   ├── FcmTokenSyncObserver.kt Process-lifetime pending-token -> backend sync
│   │   ├── FcmTokenFetcher.kt      Proactive FirebaseMessaging.token fetch
│   │   └── PassNotificationDeepLink.kt  passId/type extra contract + parsing
│   ├── session/
│   │   └── SessionManager.kt       Global SessionState (Valid/RequiresReauth) (step 3.1)
│   ├── util/
│   │   ├── SafeApiCaller.kt        Retrofit Response<T> -> ApiResult<T> mapping, injects SessionManager (step 3.1)
│   │   └── CachedNetworkFirst.kt   Shared TTL-gated caching decision tree (step 2.2)
│   └── repository/                 SatelliteRepository, PassRepository (getPasses/getPassHistory/
│                                    etc.), NotesRepository (step 2.2), AuthRepository,
│                                    SettingsRepository (step 2.3), MapRepository (Map screen)
├── domain/
│   ├── model/
│   │   ├── ApiResult.kt            Uniform outcome type for every repository call
│   │   ├── Satellite.kt, Pass.kt, Note.kt, PassTrack.kt, NotifyStatus.kt (step 2.2)
│   │   ├── UserSettings.kt         (step 2.3)
│   │   ├── TimeWindow.kt, PassHistoryFilter.kt, PagedResult.kt (Full Pass List screen)
│   │   └── SatellitePosition.kt    SatellitePosition, TrackPoint, LatLng (Map screen)
│   ├── util/                       PassFilters.kt, GeoUtils.kt (Map footprint geometry)
│   └── mapper/                     Dto <-> Entity <-> domain extension functions (step 2.2/2.3),
│                                    PassHistoryFilterMappers.kt (Full Pass List screen),
│                                    RealTimeMappers.kt (Map screen)
├── di/
│   ├── NetworkModule.kt            OkHttpClient, Json, Retrofit, SatTrakkApi
│   ├── DatabaseModule.kt           Room AppDatabase + DAOs
│   ├── ClockModule.kt              java.time.Clock, for testable "now" (step 3.1)
│   ├── CoroutineScopeModule.kt     @ApplicationScope CoroutineScope, for fire-and-forget work outliving a caller
│   ├── DataStoreModule.kt          Preferences DataStore singleton + HiddenSatellitesStore/FcmTokenStore/
│   │                                NotificationPromptStore bindings
│   ├── PermissionModule.kt         NotificationPermissionManager binding (Settings screen)
│   └── PushModule.kt               FcmTokenFetcher binding (Step 5)
├── ui/
│   ├── theme/                       Color.kt, Shape.kt, Type.kt, Theme.kt — M3 tokens from the
│   │                                design MCP (see below)
│   ├── testerentry/
│   │   ├── TesterEntryUiState.kt    Idle/Loading/NotAllowlisted/AlreadyRegistered/Error/Success
│   │   ├── TesterEntryViewModel.kt  Beta tester entry screen logic — see below
│   │   └── TesterEntryScreen.kt     Real Composable content — see below
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
│   ├── map/                         MapUiState + MapViewModel (data/logic layer), MapScreen.kt
│   │                                (MapLibre Compose UI, both flows + drawer), MapGeometry.kt
│   │                                (antimeridian/polar geometry for rendering)
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

## SessionManager — global re-authentication state (Step 3.1, extended by the Tester Entry screen)

`data/session/SessionManager.kt`:

```kotlin
sealed interface SessionState {
    object Valid : SessionState
    object RequiresReauth : SessionState
}

@Singleton
class SessionManager @Inject constructor(private val apiKeyStore: ApiKeyStore) {
    val sessionState: StateFlow<SessionState> // backed by a MutableStateFlow, initial value below
    fun markReauthRequired()
    fun markValid()
}
```

Session invalidation is modeled as **state, not a one-shot event stream** — per current official
Android guidance (state-driven UI over event-driven), so a future root composable can observe
`sessionState` and react correctly regardless of how many times it's (re)collected across
recomposition/process death, rather than consuming a single navigation event that could be missed
across a config change.

- **Initial state is read from `ApiKeyStore` at construction**, not hardcoded to `Valid`: a fresh
  install or a device with no stored key starts in `RequiresReauth` immediately, so `SatTrakkApp`
  shows the Tester Entry screen (below) on first launch without needing a first failed request to
  discover that the key is missing.
- **`SafeApiCaller` is the only writer of `RequiresReauth`**, at the exact point a 401 is mapped to
  `ApiResult.AuthRequired` (see above) — no repository or ViewModel calls `SessionManager`
  directly for that transition.
- **`markValid()` is called by `TesterEntryViewModel`** after a successful registration — the one
  caller this method was originally added for in step 3.1, now wired up. See "Tester Entry
  screen" below.

## Tester Entry screen — the beta program's entry point (Milestone E)

`ui/testerentry/` (`TesterEntryUiState.kt`, `TesterEntryViewModel.kt`, `TesterEntryScreen.kt`).
No design source exists for this screen (it was never part of the Claude Design mockups) — built
fresh in the app's existing M3 visual language (typography roles, `OutlinedTextField`/`Button`,
`MaterialTheme.colorScheme.error` for failures), matching Dashboard/Full Pass List/Settings.

**This is the beta program's tester entry point, not a general-purpose sign-up flow.** It exists
because a tester's device has no API key yet (fresh install) or the stored one stopped working
(`SessionManager.RequiresReauth`) — there is no concept of "create an account" here; the tester
either already has an email on the backend's `AllowlistedEmails` table (repo-root CLAUDE.md's beta
allowlist section) or they don't, and this screen's whole job is to resolve that against
`AuthRepository.register()` (step 2.3 — already handles saving the raw returned key internally;
this screen/ViewModel never touches `ApiKeyStore`).

- **Fields**: email (basic client-side check only — non-empty, contains `"@"` — the real
  gatekeeping is the backend's allowlist, so no deeper validation was added) and display name
  (non-empty). The submit button is disabled while either is empty or a request is in flight.

- **Three distinct outcomes, driven by `AuthRepository.register()`'s `ApiResult`**, matched on
  `ApiResult.Error.code` (never on `.message` text):
  - **`code == 403` → `TesterEntryUiState.NotAllowlisted`**: the email isn't on
    `AllowlistedEmails` at all. Message points the tester at the dev team.
  - **`code == 409` → `TesterEntryUiState.AlreadyRegistered`**: the email is allowlisted, but an
    *active* `ApiKey` already exists for it. Message points at an **admin**, not the dev team, and
    is deliberately different wording from the 403 case — these are different situations (not
    allowlisted at all, vs. allowlisted but needs a manual admin reset) and collapsing them into
    one generic "registration failed" message would leave the tester unable to tell which one
    applies to them.
  - **Any other `ApiResult.Error` code, or `ApiResult.NetworkError`, or (defensively)
    `ApiResult.AuthRequired`** (register() runs unauthenticated, so a 401 here would be a backend
    anomaly, not an expected outcome) → `TesterEntryUiState.Error(message)`, a generic retry-able
    failure. The typed fields (email/display name) are never cleared on any failure path, so the
    tester never has to retype after a failed attempt.
  - **Success (200)** → `TesterEntryUiState.Success`, and — critically — `TesterEntryViewModel`
    calls `SessionManager.markValid()` **before** setting that state. `SatTrakkApp` observes
    `SessionManager.sessionState` (not `TesterEntryUiState`) to decide whether to show this screen
    at all, so flipping `SessionManager` to `Valid` is what actually navigates the tester into
    `MainNavHost` — there is no explicit "navigate away" call inside this screen/ViewModel, and
    `TesterEntryUiState.Success` itself is typically never even rendered, since the composable
    backing this screen is torn down by `SatTrakkApp`'s recomposition at (or before) the moment
    that state would be set.

- **No self-service key-reset flow exists, by design.** The `AlreadyRegistered` (409) message
  explicitly does not offer a "reset my key" action — per the project's current beta process
  (repo-root CLAUDE.md's beta allowlist section, `POST /api/admin/reissue` is still a `501` stub),
  the only real path is an admin manually setting the stale `ApiKey.IsActive = false` in the DB and
  the tester re-running registration. Do not build a client-side workaround for this later without
  a deliberate backend-side decision first (see repo-root CLAUDE.md's warning against
  "productionizing" this beta auth mechanism).

### Wiring into `SatTrakkApp` — replaces `ReauthScreen` outright

The old placeholder `ReauthScreen` (a static "contact the dev team, no self-service flow" dead
end) is **deleted**, not kept as a fallback alongside this screen — its own doc comment described
itself as a placeholder for exactly this screen. `SatTrakkApp`'s
`SessionState.RequiresReauth -> ...` branch now renders `TesterEntryScreen()` directly instead.
Because `SessionManager`'s initial value is now read from `ApiKeyStore` at construction (see
above), this also means a fresh install lands directly on the Tester Entry screen instead of
briefly showing (or requiring a first failed call to reach) the old dead end.

### Testing

Same posture as every prior UI task: no Compose UI testing convention exists in this project
beyond the one coarse instrumented smoke test (`MainActivityTest`), and none was invented here —
flagged per the established precedent from the Dashboard/Full Pass List/Settings tasks, not
silently skipped. `MainActivityTest` itself needed no changes (it only asserts on Dashboard's top
app bar title, reached only once already-authenticated — irrelevant to this screen).
`TesterEntryViewModelTest` covers the 403/409/other-Error/NetworkError/Success mappings, that
`SessionManager.markValid()` is called exactly once on success and never on any failure path, and
that the ViewModel never touches `ApiKeyStore` (there is no `ApiKeyStore` reference reachable from
`TesterEntryViewModel` at all — the strongest assertion available is that it calls
`AuthRepository.register()` exactly once and does nothing beyond updating its own state /
`SessionManager`). `SessionManagerTest` gained two new initial-state cases (key present → `Valid`,
absent → `RequiresReauth`) for the constructor change above.

Verified in this environment: `:app:compileDebugKotlin`, `:app:testDebugUnitTest` (130 tests
green — 121 before this task, +8 in `TesterEntryViewModelTest` +1 in `SessionManagerTest`'s new
initial-state case), and `:app:assembleDebug`, all `BUILD SUCCESSFUL`. **Not verified**:
`:app:connectedDebugAndroidTest` — no `adb`/connected device or emulator was available in this
environment, same limitation noted in every prior UI task.

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
| `map?passId={passId}&satelliteId={satelliteId}` | `composable` | both optional (nullable) | **Real content** (Milestone F — see the Map UI section near the end of this file) |
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

### `SatTrakkApp` — session-state root wrapper (`navigation/SatTrakkApp.kt`)

`SatTrakkApp` is the composable root (`MainActivity.setContent { SatTrakkApp() }`). It wraps
`SatTrakkTheme`, collects `SessionManager.sessionState` via `collectAsStateWithLifecycle()` (new
dependency at the time: `androidx.lifecycle:lifecycle-runtime-compose`, alongside the existing
`lifecycle-runtime-ktx`/`lifecycle-viewmodel-ktx`), and swaps between `MainNavHost()` (the entire
nav graph) and — as of the Tester Entry screen task — `TesterEntryScreen()`. The real
`SessionManager` singleton is supplied via a small internal `AppViewModel` (`@HiltViewModel`,
`hiltViewModel()`-resolved) rather than a `sessionManager` parameter field-injected into
`MainActivity` — this was reworked when `SessionManager`'s constructor gained an `ApiKeyStore`
dependency (see "SessionManager" above), so `MainActivity` no longer needs its own
`@Inject lateinit var SessionManager` field at all.

**Originally this branch rendered a static `ReauthScreen` dead end here** (no self-service flow,
since `SessionManager.markValid()` had no caller yet) — that screen and its doc entry are now
deleted outright, replaced by `TesterEntryScreen()`, which both offers the tester a real
registration path and calls `markValid()` on success. See "Tester Entry screen" above for the
full design.

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
`PassHistoryFilterMappers.kt`'s doc comments.
**Superseded by the design-review bug-fix round (see that section near the end of this file):**
this paragraph originally said nothing yet drove a `Custom(null, null)` request in practice — that
is no longer true. `FullPassListViewModel`'s UPCOMING/HISTORY/ALL views now always query with
exactly this shape (`UNFILTERED_QUERY`) as their default, everyday behavior, which is what makes
`isFullyLoaded` a live, commonly-reached path rather than dead code waiting on a future affordance.
Do not read the "ALL filter's merge/sort logic" section below as ALL someday needing to become
filter-aware — it deliberately never will; see the FILTERED segment section instead.

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

A separate destination from Dashboard and Full Pass List. This section covers the logic layer;
see "Settings screen — Composable/UI" further below for the real Composable content built against
it. Covers three independent concerns: which satellites are hidden from the Dashboard (purely local),
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

## Settings screen — Composable/UI (Milestone E)

Replaces the placeholder from the nav-graph task with real content: `SettingsScreen.kt`
(`ui/settings/`), wired to the already-complete `SettingsViewModel`/`SettingsUiState` above, plus
one small addition to both: `SatelliteVisibility.noradId`. Driven by the code truth map's Screen
8/8 verdicts, read against the design MCP's actual "M3 Settings" screen markup (not just the
truth map's prose), plus this task's own confirmed decisions. Does **not** touch Dashboard, Full
Pass List, Pass Details, Map, or Sky View.

### `SatelliteVisibility.noradId` — a deliberate, flagged exception to "no new UiState fields"

The task's own scope said not to add new ViewModel/UiState fields, but also said to display the
NORAD id on each satellite row (truth map: "NORAD id is genuinely available here"). `SatelliteVisibility`
(`satelliteId`, `satelliteName`, `isHidden`) had no such field — only the private `loadedSatellites:
List<Satellite>` inside `SettingsViewModel` carries `noradId`, never surfaced on the UI-facing
projection. This was flagged explicitly rather than silently resolved either way; the confirmed
resolution was to add `noradId: Int` to `SatelliteVisibility` and populate it in the existing
`combine()` block alongside `satelliteId`/`satelliteName` — no new fetch, no new state stream, just
surfacing data the ViewModel already had in hand. `SettingsViewModelTest`'s existing
initial-load test gained two extra assertions for this (matching the established pattern of
extending an existing test over adding a new one for this kind of change), not a new test method.

### Three design elements omitted, per this task's confirmed decisions

- **"Minimum elevation" pass-filter slider** — omitted entirely, not even disabled. It maps to the
  backend's *global* `Settings.MinElevation` (repo-root CLAUDE.md), which `SettingsRepository`
  never touches — that repository only wraps the per-tester `/api/settings/me*` endpoints. There is
  no client-side path to this value at all.
- **"Outlook integration" card** (Connected account / Manage / schedule range / Team email CC) —
  omitted entirely. This isn't an unimplemented feature; it reflects the pre-ICS Graph-based
  "connected account" design that was explicitly replaced by the per-pass ICS-export flow (repo-root
  CLAUDE.md's Calendar Sync section). None of `CalendarSyncSettings.TeamEmail`,
  `Settings.OutlookDays`, or a "connected account" concept exists anywhere reachable from this
  screen's ViewModel, and none should be invented for it.
- **Satellite-tabs-equivalent**: none exists on this screen (there's no multi-satellite tab row
  anywhere in the Settings mockup) — nothing was omitted here beyond what the truth map already
  covered above.

Also **not a design omission but a deliberate substitution**: the design's custom-drawn pill toggle
(a filled track + circular thumb) is rendered as a standard Material3 `Switch` instead of a
hand-copied shape — consistent with how `DashboardScreen`/`FullPassListScreen` already prefer stock
M3 components (`PrimaryTabRow`, `SegmentedButton`, `FilterChip`) over reproducing the design's raw
pixels. `checked = !satellite.isHidden` (visible is the Switch's "on" position, matching the
design, since hiding is the sparse opt-out state — see `HiddenSatellitesStore` above).

### `sendPushEnabled`/`setSendPushEnabled`/`needsAlertMinutesSelection` are NOT wired here

`SettingsUiState` already exposes these (a derived "is push on at all" concept, with a
restore-last-selection flow), but neither this task's confirmed decisions nor the design mockup
itself calls for a separate push on/off control distinct from the five alert-timing chips — the
design's "Alert timing" section is just the five chips, with no separate switch above or below
them. Per this task's explicit scope ("Alert timing chips ... wired to `updateAlertMinutes()`"),
each chip's selected state is `state.alertMinutes.contains(minute)` and tapping one calls
`updateAlertMinutes(alertMinutes ± minute)` directly — a plain multi-select, not routed through
`setSendPushEnabled`. An empty selection already means "no alerts" on the backend (repo-root
CLAUDE.md), so this reaches the same end state without the extra layer. `lastNonEmptyAlertMinutes`/
`needsAlertMinutesSelection`/`sendPushEnabled` remain real, tested ViewModel state with no
Composable consumer yet — leave them as-is; they're prep for a possible future "Send push" toggle
UI, not dead code to remove.

### Push notification permission section — new UI, no design element at all

Per this task's confirmed decision #4, this section (`PermissionStatusCard`, placed below "Alert
timing") has **no** corresponding element anywhere in the source design file — built fresh, in the
app's existing M3 visual language (a `surfaceContainerLow` card matching the "Satellites" card's
own container color/shape, a leading icon, primary-colored section label matching every other
section header on this screen).

- **Status row**: `BellIcon` (a new, original glyph added to `navigation/NavIcons.kt` — the design
  has no bell/notification icon anywhere to copy from) plus "Push notifications enabled"/"not
  enabled" text, colored `primary`/`onSurfaceVariant` off `state.pushPermissionGranted` — the same
  binary-state color convention `DashboardScreen`'s `isNextPass` styling already uses, not a new
  pattern.
- **Two mutually exclusive action buttons, shown only when not granted** — `shouldShowRationale`
  decides which, per the task's explicit branching: `true` → a filled `Button` ("Enable
  notifications") that launches `ActivityResultContracts.RequestPermission()` for
  `POST_NOTIFICATIONS`; `false` → a filled `Button` ("Open app settings") that starts
  `ACTION_APPLICATION_DETAILS_SETTINGS` for this app's package. **Flagged, not silently
  resolved**: Android's `shouldShowRequestPermissionRationale` returns `false` both for "never
  asked yet" and "permanently denied" — there is no third platform signal to distinguish them, and
  `NotificationPermissionManager` doesn't track its own "have we asked before" bit (see that
  section above). This means a tester's very first visit to this screen, having never been asked
  for the permission at all, sees the "Open app settings" button rather than a direct system
  prompt. This is the literal, explicit branching this task specified, not a bug — revisit only if
  a future task decides to persist an "already asked once" bit somewhere to break the tie.
- **Refresh triggers**: an initial `LaunchedEffect(Unit)` (so a fresh navigation to this screen is
  correct immediately, since the Activity may already be resumed and never fire its own `ON_RESUME`
  for this composition), a `DisposableEffect` `LifecycleEventObserver` on `ON_RESUME` (catches the
  user granting the permission from system settings and returning), and the permission-request
  launcher's own completion callback. All three call the same
  `SettingsViewModel.refreshPermissionStatus(activity)` — never a bespoke read of
  `NotificationPermissionManager` from the Composable layer.
- The screen sources its `Activity` via `LocalContext.current as Activity`, per
  `NotificationPermissionManager.shouldShowRationale`'s own doc comment — never stored beyond the
  composition, matching that method's design intent exactly.

### "Add satellite" — tappable row + snackbar, never disabled

Per this task's confirmed decision #1: the row looks and behaves like any other tappable list
item (no dimming, no disabled affordance) and calls the existing `addSatellite()` stub on click.
The stub's `stubMessage` is surfaced via a `SnackbarHostState`/`Scaffold.snackbarHost`, the first
`Snackbar` usage anywhere in this app — a `LaunchedEffect(state.stubMessage)` shows it once and
immediately calls `consumeStubMessage()` so it doesn't reappear on a later recomposition or config
change (matching the existing `PassDetailsViewModel`/`DashboardViewModel` convention of the
Composable layer draining one-shot ViewModel state right after consuming it).

### Loading/error handling

`SettingsUiState` is a single flat data class (`isLoading`/`error`), not the sealed
Loading/Content/Error hierarchy `DashboardUiState` uses — so the screen shows a centered
`CircularProgressIndicator` while `isLoading`, and once loaded, an error-colored banner above the
sections if `error` is non-null (the ViewModel's existing per-source error message, e.g.
`"Satellites: ..."` — see `SettingsViewModel.loadInitialData`), without hiding whichever section
did load successfully. This mirrors `FullPassListScreen`'s non-blocking error banner, not
`DashboardScreen`'s whole-state `Error` branch (there is no whole-state error concept on
`SettingsUiState` to branch on).

### Testing

Same posture as the Full Pass List task: no Compose UI testing convention exists in this project
beyond the one coarse instrumented smoke test (`MainActivityTest`), and none was invented here.
`MainActivityTest` itself needed no changes — it only asserts on Dashboard's top app bar title,
which this task never touches. `SettingsViewModelTest` gained two new assertions (NORAD id
flowing through `combine()`) on the existing initial-load test; no new test file was added for
`SettingsScreen.kt` itself, consistent with the same gap flagged (not silently worked around) in
the Dashboard and Full Pass List tasks.

Verified in this environment: `:app:compileDebugKotlin`, `:app:testDebugUnitTest` (all 121
existing unit tests green — the count is unchanged from before this task, since the only test
change was two extra assertions on an existing test method, not a new one), and
`:app:assembleDebug`, all `BUILD SUCCESSFUL`. **Not verified**: `:app:connectedDebugAndroidTest`
— no `adb`/connected device or emulator was available in this environment, same limitation noted
in the Dashboard task.

---

## Pass Details Modal — pass detail + notes editing (Milestone E, Step 3 complete)

Set up as a `passdetails/{passId}` screen destination by the nav scaffolding, but functions as a
modal dialog over the Dashboard/Full Pass List, not a full-screen navigation target. Builds
entirely on the existing `PassRepository` (`getPassById`, `setNotify`), `NotesRepository`
(`getNotes`/`createNote`/`updateNote`/`deleteNote`), and — added in the Composable/UI task below —
`SatelliteRepository` (`getSatellites`) — no new repository methods, no backend changes.
`PassDetailsUiState`/`PassDetailsEvent`/`PassDetailsViewModel` (`ui/passdetails/`) cover the
logic; `PassDetailsScreen.kt` covers the real Composable content (see "Pass Details Modal —
Composable/UI" below). Covered by `PassDetailsViewModelTest`. **This closes out Step 3's
ViewModel/UiState layer in full** — Dashboard, Full Pass List, Settings, and Pass Details are now
all done at the ViewModel/UiState level.

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

### Full-state error on `getPassById` failure vs. partial content on `getNotes`/satellite failure

Pass, notes, and (as of the Composable/UI task) the satellite catalog are loaded in parallel on
`init` (same `async`/`awaitAll` shape as `DashboardViewModel`'s per-tab loading and
`FullPassListViewModel.loadAll`), but a `getPassById` failure is handled asymmetrically from the
other two, deliberately:

- **`getPassById` fails → the entire `PassDetailsUiState` becomes an error state**: `pass` stays
  `null`, `error` is set, and `notes`/`satelliteName`/`satelliteNoradId` are all left at their
  empty/null defaults even if the other two calls succeeded in parallel — their results are
  discarded outright. Rationale: without the pass itself (AOS/LOS/elevation/notify — the primary
  content this whole modal exists to show), there's not enough left to justify rendering anything.
- **`getPassById` succeeds but `getNotes` and/or the satellite lookup fails → partial content**:
  `pass` is shown normally, the failed side's field(s) stay at their empty/null defaults, and
  `error` describes whichever failed (notes checked first, satellite lookup second, if both fail).
  Rationale: notes and the satellite name/NORAD id are both secondary/supplementary content —
  losing either for one load shouldn't hide the primary content the user actually opened this
  modal to see.

This is the opposite asymmetry from `DashboardViewModel`'s per-tab philosophy (there, *every*
tab's own failure is isolated and never blanks the *other* tabs) — here `getPassById` is strictly
primary over the other two, which is why its failure mode blanks everything and theirs don't.

### Satellite name/NORAD id resolution — a confirmed gap, closed the same way Dashboard/Settings do it

Before the Composable/UI task, `PassDetailsUiState` had no satellite lookup at all — `Pass` only
carries an opaque `satelliteId`, so the screen had no way to show a satellite name or NORAD id.
This was a real, flagged gap (per the code truth map's Screen 7/8 verdict), not silently resolved:
`PassDetailsViewModel` now also fetches `SatelliteRepository.getSatellites()` in the same parallel
`async`/`awaitAll` batch as `getPassById`/`getNotes`, and resolves `satelliteName`/
`satelliteNoradId` by matching `Satellite.id == Pass.satelliteId` — the exact same "fetch the
24h-TTL, Room-cached full catalog and find-by-id" pattern Dashboard and Settings already use,
rather than inventing a new by-id repository method. If the id has no match in the catalog (should
not happen for a valid pass, but not asserted against), both fields simply stay `null` with no
error — only an actual fetch failure (`NetworkError`/`Error`/`AuthRequired`) sets `error`.

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

## Pass Details Modal — Composable/UI (Milestone E)

Replaces the placeholder from the nav-graph task with real content: `PassDetailsScreen.kt`
(`ui/passdetails/`), wired to the already-complete `PassDetailsViewModel`/`PassDetailsUiState`
above, per the code truth map's Screen 7/8 verdicts and this task's confirmed decisions. Does
**not** touch Dashboard, Full Pass List, Settings, Map, or Sky View. **This completes Step 3's
full screen set** (Dashboard, Full Pass List, Settings, Pass Details) — Map and Sky View remain
placeholders pending Milestone F (steps 6/7).

### Renders as a self-sized card, not the stock `AlertDialog`-width dialog

`PassDetails` was already registered as a `dialog(...)` nav destination (from the nav-graph task),
but with no `DialogProperties` of its own it inherited Compose Navigation's default
`usePlatformDefaultWidth = true`, which caps a dialog's content to the narrow, wrap-content
`AlertDialog` width — too narrow for AOS/LOS + a 5-cell metric grid + a notes list.
`SatTrakkNavHost`'s `dialog(...)` call for this route now passes
`DialogProperties(usePlatformDefaultWidth = false)`, and `PassDetailsScreen` itself centers a
`Surface` card (`Modifier.fillMaxWidth().heightIn(max = 640.dp)`) with its own internal
`verticalScroll` — this is the "should float over whatever's behind it, not assume full-screen
chrome" instruction made concrete: no `TopAppBar`, no `Scaffold` at the top level, just a plain
header `Row` (back arrow + satellite name/pass-id/NORAD-id + max-elevation chip) inside the card.
This is the only touch to `SatTrakkNavHost.kt` in this task, scoped to the one `dialog(...)` call.

### Header ground-track sparkline — on hold, not built

Per the confirmed decision, the design's header sparkline is deliberately **not** implemented,
despite `PassRepository.getPassTrack()` being fully built and available — this is a "nice to
have, later" call for a future UI polish pass, not a gap. No placeholder graphic is rendered
either; the element is omitted from the layout entirely, exactly as instructed.

### "Export to calendar (ICS)" — a stub, same pattern as Settings' "Add satellite"

The design's "Add to Outlook" button is relabeled "Export to calendar (ICS)" (matching the app's
actual ICS-based calendar flow — see repo-root CLAUDE.md's Calendar Sync section — not the old
Graph-based "connected account" wording the design implies). No `CalendarRepository` exists yet on
the Android side (a confirmed gap, to be built in a separate future task) — per the confirmed
decision, this is built the same way `SettingsViewModel.addSatellite()` is stubbed:
`PassDetailsViewModel.exportToCalendar()` only sets the new `PassDetailsUiState.stubMessage`
field ("Exporting to calendar isn't available yet"), consumed via `consumeStubMessage()` and
surfaced as a transient `Snackbar` in `PassDetailsScreen`, immediately cleared so it doesn't
reappear on a later recomposition. The button itself is a normal, always-enabled, tappable
`Button` — never disabled/grayed out — and calls no repository.

### "Notify me" switch — a per-pass boolean, not a duplicate of the global alert-minutes setting

The design shows a picker-style "Set alert" row ("15 minutes before AOS" with a chevron), implying
per-pass minute selection — but the real backing (`PassDetailsViewModel.toggleNotify()`/
`Pass.notify`) is a plain boolean. Per the confirmed decision, this is built as a standard M3
`Switch` labeled "Notify me about this pass," with **no** minute picker and **no** attempt to
expose or duplicate the tester's actual alert-timing preference (5/10/15/30/60 minutes), which is
a separate, tester-global setting already configured on the Settings screen
(`SettingsUiState.alertMinutes`). Toggling the switch calls `toggleNotify()` directly; per that
method's own no-optimistic-update design, the switch's visual state only changes once the
repository call confirms it.

### Notes — the already-built dialog-based multi-note CRUD, not the design's single inline field

Per the confirmed decision, the notes section uses `PassDetailsUiState.notes` (list, read-only
display) + `editingNote` (`EditingNoteState`) exactly as already implemented in the ViewModel, not
the design's single inline field with a char counter:

- Each note renders as a row (content + a `TrashIcon` delete affordance); tapping the row (not the
  delete icon) opens the edit dialog in `ExistingNote` mode with its content pre-filled. An "Add
  note" text button opens the same dialog in `NewNote` mode.
- The dialog itself (`NoteEditDialog`, a standard M3 `AlertDialog`) holds its typed content in
  local `remember(editingNote) { mutableStateOf(...) }` state, seeded from `editingNote`'s current
  content — `saveNote(content)` fires on "Save," `closeNoteDialog()` on "Cancel" or dismiss.
  "Save" is disabled while the field is blank. Because `PassDetailsScreen` is itself already
  hosted inside an Android `Dialog` window (the `dialog(...)` nav destination), this nests a second
  Compose `Dialog` on top — standard, supported behavior, not a new pattern.
- No "visible to your team" labeling or character counter is built — `Note` has no such fields
  (per the confirmed decision and the truth map's own `[DECORATIVE]` verdict for both).
- `TrashIcon` (`navigation/NavIcons.kt`) is a new original glyph — the design has no delete
  concept on its single-field note UI to copy from.

### "Show on map" — wired to the app's first Channel-based event consumer

`PassDetailsScreen` collects `PassDetailsViewModel.events` via `LaunchedEffect(Unit) {
viewModel.events.collect { ... } }` and, on `PassDetailsEvent.NavigateToMap`, calls the screen's
`onNavigateToMap` callback. `SatTrakkNavHost` wires this to pop the modal off the backstack and
navigate to `SatTrakkDestination.Map` — since Map is still an empty placeholder screen (Milestone
F), this lands on that placeholder today, which is expected and correct, not a gap to fix here.
**Superseded:** it now opens Map's Flow 2 for this pass (`navigateToMap(passId = ...)`) — see the
Map UI section near the end of this file.

### Loading/error states — distinct, not collapsed into one generic view

Per `PassDetailsUiState`'s own already-implemented asymmetry (see above): `state.isLoading` shows
a centered `CircularProgressIndicator` inside the card (header still rendered, so the back arrow
stays available even while loading); `state.pass == null` with loading finished shows a full-card
error message (the `getPassById`-failure case); otherwise the real content renders, with a
non-blocking inline error banner above the AOS/LOS row if `state.error` is set from a
notes/satellite-lookup failure (the partial-content case) — mirroring `FullPassListScreen`'s and
`SettingsScreen`'s existing non-blocking-banner convention, not `DashboardScreen`'s whole-state
`Error` branch.

### Metric grid, AOS/LOS formatting — local duplicates, not shared with Dashboard

`MetricCell`/`MetricGrid` in `PassDetailsScreen.kt` are a local copy of `DashboardScreen.kt`'s
equivalent private composables (same card style/typography), and the date/time formatting helpers
are local, small functions — not factored into a shared file, consistent with
`FullPassListScreen.kt`'s own established precedent for the same reason (this task's scope
excludes touching Dashboard). AOS/LOS are shown in both local and UTC (`formatTimeLocal`/
`formatTimeUtc`); the single date line above them uses AOS's own local date, since a pass never
spans a UTC day boundary long enough to make that ambiguous in practice. TLE epoch is omitted
entirely, unchanged from the truth map's verdict — `Pass` only carries an opaque `tleId`, with no
epoch data available client-side.

### Testing

Same posture as every prior UI task: no Compose UI testing convention exists in this project
beyond the one coarse instrumented smoke test (`MainActivityTest`), and none was invented here.
`MainActivityTest` itself needed no changes — it only asserts on Dashboard's top app bar title,
unaffected by this task. `PassDetailsViewModelTest` gained four new test methods for this task's
one real ViewModel change (satellite name/NORAD id resolution) and its stub methods: a satellite-
lookup-failure case, a no-matching-satellite case, and `exportToCalendar()`/`consumeStubMessage()`
— plus the existing "successful load" and "getPassById failure" tests gained extra assertions for
the new `satelliteName`/`satelliteNoradId` fields, rather than duplicating those as new tests.

Verified in this environment: `:app:compileDebugKotlin`, `:app:testDebugUnitTest` (134 tests
green — 130 before this task, +4 in `PassDetailsViewModelTest`), and `:app:assembleDebug`, all
`BUILD SUCCESSFUL`. **Not verified**: `:app:connectedDebugAndroidTest` — no `adb`/connected device
or emulator was available in this environment, same limitation noted in every prior UI task.

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

---

## Milestone E — UI bug-fix and enhancement round (post design-review findings)

A full manual walkthrough of the four built screens (Dashboard, Full Pass List, Settings, Pass
Details) surfaced several bugs and one product gap, fixed in this round. This section is the
canonical description of each fix; it supersedes any earlier wording elsewhere in this file that
conflicts with it (the "ALL filter's merge/sort logic" section above has an explicit pointer back
here). Assumes the backend's notify-default flip (opt-in, not opt-out — see repo-root CLAUDE.md's
`PassSubscription` section) is already merged.

### Shared "exclude past AOS" filter — `domain/util/PassFilters.kt`

Both Dashboard and Full Pass List independently had the same bug: a pass fetched while still
upcoming can have its AOS pass while the TTL-gated cache that produced it (`PassRepository
.getPasses`, 1h TTL) is still considered fresh, so it kept showing as "upcoming" until the next
network refresh. `List<Pass>.excludePastAos(now)` (inclusive of exactly `now` — a pass whose AOS
is this instant still counts as upcoming) is the single shared utility both ViewModels call; it is
**not** applied to what gets fetched or cached, only to what each ViewModel derives for display,
so it has to be recomputed against the current time at every state-computation point, not baked in
once.

- **`DashboardViewModel`**: `SatelliteTabState` gained a `visiblePasses: List<Pass>` field,
  computed alongside the existing raw `passes` field at every point that produces one (`loadTab`,
  `applyPassesResult`, and — critically — every tick of the existing per-second countdown ticker
  for the *selected* tab, so the rendered list keeps shrinking live as passes' AOS arrive, not just
  on the next 5-minute poll). `passes` itself is left untouched as the raw fetched/cached list, so
  "the cache should still hold the full fetched set" holds — `DashboardScreen` was changed to
  render `visiblePasses`, not `passes`.
- **`FullPassListViewModel`**: applied at the one place the "upcoming" portion is actually sourced
  from `PassRepository.getPasses` — `loadUpcomingOnly()` and `loadAll()`'s upcoming half. History
  is unaffected (it's explicitly about the past, so nothing there can go "stale-upcoming"). Since
  this screen has no per-second ticker, the filter is only as fresh as the last
  reload/filter-change, matching this screen's existing tolerance for relative-time staleness
  between recompositions (see its own Testing section).
- Covered directly by `PassFiltersTest` (past/future/exactly-now boundary cases, inclusive), plus
  `DashboardViewModelTest`'s countdown-rollover test asserting `visiblePasses` drops a pass once
  its AOS arrives while `passes` still carries it.

### DashboardViewModel now observes `HiddenSatellitesStore`

`DashboardViewModel` didn't exist yet when `HiddenSatellitesStore` was built (Settings screen task)
— only `SettingsViewModel` was ever wired to it. Hiding a satellite in Settings persisted correctly
but Dashboard's tabs never reflected it while already running.

- `DashboardViewModel` gained a `HiddenSatellitesStore` constructor dependency (Hilt resolves it
  automatically — the binding already existed in `DataStoreModule`). The internal engine
  (`_rawState`) keeps working exactly as before, holding **every** loaded tab regardless of hidden
  status — every existing load/poll/ticker method is unchanged. The publicly exposed `uiState` is
  a `combine(_rawState, hiddenSatellitesStore.hiddenSatelliteIds) { ... }.stateIn(...)` that
  filters hidden satellites' tabs out at the very last step.
- This two-state split (internal raw engine + a `combine()`-derived public projection) is
  deliberate, not incidental complexity: writing hidden-filtered tabs directly into a single state
  object would mean a later poll/refresh/ticker tick — which only knows the raw fetched data, not
  the current hidden set — silently un-hides a satellite the next time it writes state. `combine()`
  re-derives the filtered view from both inputs every time, so that race can't happen.
  `SettingsViewModel`'s own `combine()` doesn't have this problem because `satellites` there is
  *exclusively* combine-derived, never written from anywhere else — Dashboard's `tabs` is written
  from many places, hence the split.
  - If the currently *selected* satellite becomes hidden while Dashboard is active, a dedicated
    collector (`observeHiddenSatellites`) reacts by picking the first still-visible tab as the new
    selection and explicitly restarting the countdown ticker for it — without this, the ticker
    would keep running for the now-hidden tab and the newly-selected (but never-ticked) tab's
    countdown/hero card would stay frozen at whatever it last was (usually nothing).
  - `loadDashboard()` also snapshots the current hidden set once up front (`hiddenSatelliteIds
    .first()`) purely to avoid choosing an already-hidden satellite as the *initial* default
    selection (e.g. a satellite hidden in a previous session that also happens to be
    `Satellite.isDefault`) — the reactive collector above only fires on a *change*, so it doesn't
    by itself fix a bad initial pick made before Dashboard ever had a live subscription running.
- Covered by two new `DashboardViewModelTest` cases: hiding a non-selected satellite's tab
  disappears with zero additional repository calls (purely reactive to the Flow), and hiding the
  *selected* satellite falls back to another tab with its countdown correctly populated.

### The FILTERED segment (supersedes the earlier "ALL will need filter-awareness" framing)

Confirmed product decision, not a bug fix: rather than making UPCOMING/HISTORY/ALL read the Filter
Modal's `timeWindow`/`minMaxElevation`, those three stay **permanently unfiltered, pure time-based
views** — a new fourth segment, `PassListFilter.FILTERED`, is the *only* place any filter (current
or future) ever takes effect. This directly fixes the min-elevation-filter-has-no-effect-on-
Upcoming bug found in review, by removing the premise that UPCOMING/ALL should ever have read the
filter fields in the first place.

- **UPCOMING/HISTORY/ALL always query with `UNFILTERED_QUERY`** (`PassHistoryFilter
  (TimeWindow.Custom(null, null), null)`, a private `FullPassListViewModel` companion constant),
  never `state.timeWindow`/`state.minMaxElevation` — this is a real behavior change from before
  this round, when HISTORY/ALL used whatever the Filter Modal's fields happened to hold (defaulting
  to `Last7Days`). One direct consequence: HISTORY now naturally paginates through a satellite's
  **entire** retained history rather than being artificially capped to a rolling default window,
  which is also what makes `PassRepository.getPassHistory`'s `HistoryLoadState.isFullyLoaded`
  bookkeeping a live, commonly-exercised path instead of the "nothing drives this yet" dead code
  the section above used to describe.
- **Auto-activation**: `setTimeWindow`/`setMinMaxElevation` no longer call `reload()` directly —
  they go through `applyFilterActivation()`, which compares the new field values against
  `DEFAULT_TIME_WINDOW`/`DEFAULT_MIN_MAX_ELEVATION` and sets `filter` to `FILTERED` if either is
  now non-default, or back to `ALL` if a change brought both back to default. This fires from
  *any* starting segment (UPCOMING/HISTORY/ALL/already-FILTERED) — picking a filter value is what
  activates FILTERED, not a separate user action.
- **Auto-return to ALL**: `resetFilters()` only reloads (and only switches segments) when the
  current segment was `FILTERED` — resetting the fields while on UPCOMING/HISTORY/ALL just clears
  them with no reload, since those views never read them anyway (their currently-shown data can't
  be stale with respect to fields they never queried with). Directly clearing a field back to its
  default value via `setTimeWindow`/`setMinMaxElevation` (not through `resetFilters()`) also
  triggers the same FILTERED→ALL fallback via `applyFilterActivation`'s own default-check — there's
  only one place "is a filter active" is decided.
- **Mixed chronology in one query, not two**: FILTERED reuses the exact same call shape
  `loadHistoryOnly()` already used (`PassRepository.getPassHistory(satelliteId, page,
  <filter>)`), just with the user's real filter instead of `UNFILTERED_QUERY`. Since that backend
  endpoint (and its local Room-fresh-and-fully-loaded fallback) has no concept of "only past" — it
  returns every stored pass matching the aos/elevation bounds, upcoming or historical alike —
  reusing it here is what actually performs the mixed-chronology filtered query ALL's own name was
  incorrectly implying it did. No new repository method and no new caching strategy were added:
  Room-first-then-network is inherited for free from `getPassHistory`'s existing decision tree,
  already covered end-to-end by `PassRepositoryHistoryTest`. `nearestPassId` stays `null` for
  FILTERED — unlike ALL, it isn't pasting together two independently-fetched portions with a
  boundary to mark, it's one already-sorted, already-homogeneous result.
- **Composable**: the segmented control (`FullPassListScreen`) computes the same "is a filter
  active" check as the ViewModel (comparing against `FullPassListViewModel
  .DEFAULT_TIME_WINDOW`/`.DEFAULT_MIN_MAX_ELEVATION`, the same public constants the badge-count/
  active-filter-chip logic already used) and only includes `FILTERED` in the row while that's
  true — it isn't a permanently-visible fifth option sitting empty. Picking any filter value in
  `FilterModalSheet` (a time-window chip, or the elevation slider's release) now also closes the
  sheet immediately, wired at the `FullPassListScreen` call site (`onSetTimeWindow`/
  `onSetMinMaxElevation` both call `showFilterSheet = false` after the ViewModel setter) —
  matching "closing the filter sheet" being part of what selecting a filter value does.
- Covered by several new `FullPassListViewModelTest` cases: activating FILTERED from ALL and from
  a non-ALL starting segment, FILTERED returning a mixed future+past result from one query,
  resetting from FILTERED back to ALL (and reloading it), resetting while not on FILTERED being a
  no-op reload-wise, directly clearing a field back to default also falling back to ALL, and
  FILTERED's own pagination resetting to page 1 on a filter-field change while never touching
  `getPasses`.

### Nearest-pass highlighting in the ALL view (`FullPassListScreen`)

`FullPassListUiState.nearestPassId` was already correct (existing tests already covered its
computation) but had no visual treatment at all. `PassRow` gained an `isNearest: Boolean` param —
`state.filter == PassListFilter.ALL && pass.id == state.nearestPassId` — applying the same
secondaryContainer background/text-color treatment `DashboardScreen.PassRow` already uses for its
"next pass" row, rather than inventing a new visual language. Deliberately not wired for
UPCOMING/HISTORY/FILTERED — `nearestPassId` is always `null` outside ALL anyway (see the ALL
merge-logic section above), and has no meaning there even if it weren't.

### Pull-to-refresh on Dashboard actually does something now

Root cause: `DashboardViewModel.refresh()` was correctly implemented from the original Dashboard
task, but **no gesture handler in `DashboardScreen` ever called it** — there was no
`PullToRefreshBox`/`pullRefresh` anywhere in the file, so the pull gesture had nothing to trigger
it. Not a broken `refresh()`, not a silent exception — the wiring simply never existed.

- Fixed by wrapping `DashboardScreen`'s Scaffold content in `androidx.compose.material3
  .pulltorefresh.PullToRefreshBox`, `onRefresh = viewModel::refresh`.
- `DashboardUiState.Content` gained an `isRefreshing: Boolean = false` field for the spinner to
  bind to — there was previously no state at all to represent "a refresh is in flight" (`refresh()`
  just fired and let the normal `applyPassesResult` path update `passes`/`loadError`, with no
  distinct flag). `refresh()` sets it `true` before launching and `false` once the request settles
  (success or failure either way).
- Not covered by a Compose UI test — no such test infrastructure exists anywhere in this project
  (see every prior UI task's own Testing section) and none was invented here; flagged rather than
  silently skipped. `DashboardViewModelTest`'s existing refresh test was extended to assert
  `isRefreshing` returns to `false` after the call settles.

### Navigation debounce — Pass Details, both call sites

Root cause: neither Dashboard's nor Full Pass List's row-tap-to-PassDetails call site had any
guard against rapid repeated taps, and Compose Navigation doesn't inherently prevent duplicate
rapid navigation to the same destination — each tap fired `navController.navigate(...)`
independently, opening multiple stacked modal instances.

Fixed once, centrally, in `SatTrakkNavHost.kt`: a private `NavHostController.navigateDebounced
(route: String)` extension only calls `navigate(route)` when `currentBackStackEntry?.lifecycle
?.currentState == Lifecycle.State.RESUMED` — the standard Compose Navigation pattern for this
exact problem (a second tap arriving before the first navigation finishes finds the entry already
past `RESUMED`, e.g. `STARTED` while the new destination composes, and is silently ignored). Both
`DashboardScreen`'s and `FullPassListScreen`'s `onPassClick` call sites in `MainNavHost` were
switched from `navController.navigate(...)` to `navController.navigateDebounced(...)` — the FAB's
navigation to Map and the bottom nav bar's top-level navigation were left untouched, since rapid-
tap duplication was never reported for those and `navigateToTopLevel`'s own `launchSingleTop`
already guards the top-level case differently.

Not covered by an automated test — verifying tap-debounce behavior needs Compose UI testing
(`ComposeTestRule` + synthetic gesture events) against a real `NavHostController`, and no Compose
UI test infrastructure exists anywhere in this project (same gap flagged in every prior UI task).
Flagged explicitly per this round's own instructions, rather than skipped silently or invented
ad hoc.

### Client-side notify-initialization workaround — confirmed absent, nothing to remove

This round's scope explicitly forbade adding (or required removing, if present) any client-side
logic that writes a `notify = false` row when Pass Details opens a "new" pass, since that pattern
was only ever a workaround for the backend's old opt-out notify default — now flipped to opt-in
(repo-root CLAUDE.md's `PassSubscription` section). Checked `PassDetailsViewModel` directly: no
such logic exists anywhere in `loadInitialData()` or `toggleNotify()` — the pass's `notify` value
has only ever been read from `PassRepository.getPassById`'s result and never proactively written
on load. Nothing was removed because nothing like this was ever built.

### Timezone conversion — consolidated into `ui/common/DateTimeFormatting.kt`

Investigated the reported "AOS/LOS local time not actually converted, shows UTC twice" bug
directly against `DashboardScreen`, `FullPassListScreen`, and `PassDetailsScreen`'s own
`formatTimeLocal` implementations: **all three already called `dateTime.atZoneSameInstant
(ZoneId.systemDefault())` correctly** — none of them were relabeling a UTC value under a "local"
heading. No actual conversion defect was found in the shipped code.

What *did* exist was three independent, near-identical private copies of the same formatting
logic, with no way to test the conversion deterministically (each hardcoded `ZoneId.systemDefault
()`, tying any test to whatever timezone the test runner happens to be in). Consolidated into
`ui/common/DateTimeFormatting.kt` (`formatTimeLocal`/`formatTimeUtc`/`formatDateLocal`), each
taking an explicit `zone: ZoneId = ZoneId.systemDefault()` parameter so `DateTimeFormattingTest`
can assert against a fixed zone (`America/New_York`) instead of depending on the host's actual
timezone. All three screens now import from this shared file instead of defining their own copy;
`PassDetailsScreen` is the only one that also uses `formatTimeUtc`/`formatDateLocal`. The date
formatters are pinned to `Locale.US` (not the device default) — the patterns render English-only
abbreviations regardless, and pinning avoids a formatter whose exact output (e.g. "Sep" vs "Sept")
silently depends on the JDK/ICU version running it, which is what made the first version of
`DateTimeFormattingTest` non-deterministic across JDKs before this pin.

---

## Milestone E — round 2: UI bug-fix + diagnostics round

A second manual walkthrough found that several round-1 fixes did not actually take effect, plus
one new bug (RUNNER-1's Full Pass List). Each item below required diagnosing *why* the prior round
didn't fix it, not just re-applying a fix — recorded here in detail so this class of "partial fix"
problem is traceable later instead of silently recurring a third time.

### 1a. Timezone conversion — reconfirmed correct, no code defect found (again)

Re-investigated from scratch against the current `ui/common/DateTimeFormatting.kt` and all three
call sites (Dashboard, Full Pass List, Pass Details): every one still calls
`dateTime.atZoneSameInstant(zone)` correctly, exactly as round 1 already established, and
`DateTimeFormattingTest`'s fixed-zone (`America/New_York`) assertions continue to prove this
deterministically, independent of the host's own timezone. No second conversion bug was found, and
no code changed for this half of the ticket. The most likely explanation for "still shows UTC
twice" on manual QA is that the **test device/emulator's own system timezone was set to UTC** (a
common default for a fresh AVD), which makes the local and UTC lines legitimately identical to
look at — that's a test-environment artifact, not a reproducible defect in the app. If this is
reported again, the first thing to check is the test device's own `Settings > Date & time` zone,
not the formatting code.

### 1b. Metric-grid centering — never actually fixed in round 1 (this was the real gap)

Root cause: round 1's only commit touching this area (`4255e5f`, "Consolidate local/UTC time
formatting") is scoped entirely to timezone conversion — its own commit message explicitly frames
it as "this file doesn't fix a conversion defect; it consolidates the three duplicates." It never
touches `PassDetailsScreen.kt`'s `MetricGrid` layout, and no other commit in the project's history
ever does either. The two complaints (timezone + centering) were evidently tracked as one ticket,
but only the timezone half was ever diagnosed and fixed — the centering half was silently dropped,
not fixed-and-regressed.

The actual layout defect: `PassDetailsScreen`'s metric grid has 5 cells (Duration/AOS az/LOS
az/Orbit/Max elev per the design), which doesn't divide evenly into a 3-column row, so it renders
as a 3-cell row followed by a 2-cell row. The 2-cell row's cells each used
`Modifier.weight(1f)` *within that row alone*, which stretched them to half the full row width
apiece — wider than the 3 equal columns above, making the grid look uneven/off-center rather than
one coherent 3-column grid with a short last row. Fixed by giving the second row the same 3-column
proportions as the first: two real cells plus a `Spacer(Modifier.weight(0.5f))` on each side, so
the pair lines up under two of the three columns above and reads as centered.

### 2. Notify toggle disabled for historical passes

`PassDetailsViewModel` gained a `Clock` constructor dependency (same pattern as
`DashboardViewModel`'s, see that section above) and `PassDetailsUiState.isHistorical: Boolean`,
computed once at load time as `pass.los.isBefore(OffsetDateTime.now(clock))`. `toggleNotify()`
itself now no-ops (no repository call) when `isHistorical` is true, as a server-side-of-the-UI
guard in addition to the Composable's own `enabled = false` — belt and suspenders, since notifying
about a pass whose LOS already passed has no effect either way. `PassDetailsScreen`'s `NotifyRow`
takes a new `enabled` param: `false` dims the label color and disables the `Switch`
(`enabled = false`), while `checked` still reflects the pass's actual stored `notify` value
unconditionally — the switch is never hidden, only made non-interactive, per this task's
requirement. Not recomputed on a ticker (this screen has no per-second ticker, unlike Dashboard) —
computed once at load, which is correct since a pass already in Pass Details won't cross its own
LOS boundary while the modal is open in any realistic session length.

### 3. Navigation debounce — root cause found in androidx.navigation's own source, not a drop-in fix

Round 1's guard (`if (currentBackStackEntry?.lifecycle?.currentState == RESUMED) navigate(route)`)
did not stop duplicate modals, and was applied consistently at both (the only two) row-tap call
sites already — grep-verified against `SatTrakkDestination.PassDetails.buildRoute` call sites
across the whole codebase; Dashboard and Full Pass List remain the only two, no third site exists.
So the guard's own *logic* was the problem, not its coverage.

Root cause, confirmed by reading `navigation-compose`/`navigation-runtime` 2.9.7 sources directly
(`NavControllerImpl.updateBackStackLifecycle`, `DialogNavigator`, `DialogHost`): the guard's
premise — "a second tap arriving before the first navigation finishes finds the current entry not
yet RESUMED" — only holds for plain `composable()` destinations, where Compose Navigation gates a
newly-pushed entry's promotion to `RESUMED` on its enter transition (`AnimatedContent`) actually
completing. That gives a real, if short, window during which a repeat tap's
`currentBackStackEntry` check correctly fails and gets dropped. **`PassDetails` is registered as a
`dialog()` destination**, and `DialogNavigator.navigate()` pushes its entry directly with no
transition to gate on — `updateBackStackLifecycle()` promotes a plain top-of-stack entry to
`RESUMED` synchronously, within the same `navigate()` call, regardless of destination type. So by
the time a second tap's click handler runs (a separate frame/event, not the same call stack as the
first), the just-pushed dialog entry is *already* `RESUMED`, the guard's check passes again, and a
second `PassDetails` instance gets pushed. The guard's protective window is real for `composable()`
targets but is effectively zero for `dialog()` targets — which is exactly what every
row-tap-to-PassDetails call site in this app navigates to, so the guard provided no protection at
all in practice for its own designed use case.

Fixed by dropping the lifecycle-timing heuristic entirely in favor of `NavController`'s own
built-in dedup: `navigate(route) { launchSingleTop = true }`. `NavControllerImpl
.launchSingleTopInternal` compares the target destination against `currentBackStackEntry`
synchronously, inside the same `navigate()` call, with no dependency on animation/lifecycle timing
at all — if `PassDetails` is already the current top entry, its existing entry's args are updated
in place instead of a new one being pushed. This works identically for `dialog()` and
`composable()` destinations, so it isn't fragile to a future destination-type change either.

**Not covered by an automated test** — verifying this needs either a real `NavController`
(requires Robolectric or an instrumented test; this project has neither `navigation-testing` nor
Robolectric as a dependency) or Compose UI testing infrastructure (still doesn't exist anywhere in
this project, per every prior UI task's own Testing section). Flagged explicitly per this task's
own instructions rather than silently skipped; adding either dependency would be a deliberate,
separate decision, not a quick addition to this round.

### 4. Full Pass List — scroll position now resets on a real reload, preserved across bottom-nav return

Root cause: `rememberLazyListState()` is `rememberSaveable(saver = LazyListState.Saver) { ... }`
with no keys, so it kept the exact same instance (and scroll offset) for as long as
`FullPassListScreen` stayed in composition — including across a segment switch
(Upcoming/History/All/Filtered) or picking a new filter value, both of which call
`FullPassListViewModel.reload()` and rebuild `passes` from scratch. The old scroll offset was left
pointing at whatever position it was in the *previous* list, which could be partway down or past
the end of the freshly-reloaded one.

Fixed by keying the `rememberSaveable` on exactly the three fields whose change means "this is a
new query" (`state.filter`, `state.timeWindow`, `state.minMaxElevation` — the same three
`FullPassListViewModel.reload()` itself reacts to): `rememberSaveable(state.filter,
state.timeWindow, state.minMaxElevation, saver = LazyListState.Saver) { LazyListState() }`. A
change to any of them now produces a brand-new `LazyListState` starting at index 0; a page
appended by `loadMore()` (which changes `state.passes` but none of those three fields) keeps
reusing the same instance and its scroll position, as it should.

Judgment call on "returning to an already-open instance" (not explicitly specified by this task):
Full Pass List is reachable via the bottom nav bar's "Passes" item, which uses
`navigateToTopLevel`'s `launchSingleTop = true` + `restoreState = true`/`saveState = true` —
Compose Navigation's standard bottom-nav-tab pattern. Chose to **preserve** scroll position for
that path (this is simply `rememberSaveable`'s normal restore behavior for the current
filter/segment combination, not something added specially) — matching the everyday convention that
switching bottom-nav tabs and coming back leaves each tab where you left it. Entering *fresh* (via
Dashboard's "View all" button, a plain `navigate()` with no `restoreState`) already started at the
top before this fix and still does, since a new `NavBackStackEntry` gets its own fresh
`SaveableStateHolder` scope with nothing to restore.

Not covered by an automated test — verifying actual `LazyListState` scroll-offset behavior needs
Compose UI testing infrastructure, which doesn't exist in this project (same gap flagged in every
prior UI task's Testing section).

### 5. RUNNER-1's Full Pass List showing completely mixed Upcoming/History — root cause confirmed

Diagnosed in the order this task specified. (b) and (c) were ruled out directly: `excludePastAos`
(`domain/util/PassFilters.kt`) is a simple, already-tested inclusive-of-`now` filter with no
satellite-specific edge case to trip, and `computeNearestPassId`'s boundary logic is unrelated to
the History-only segment (`nearestPassId` is always `null` outside `ALL`, and the bug reproduced on
the plain History segment too). (d) wasn't needed to explain the symptom once (a) was confirmed
against the actual source below, though a live-device data spot-check remains unverified (no
`adb`/connected device available in this environment — same limitation noted throughout this file).

**(a) was the real cause, refined**: it isn't an off-by-one in `hasMore`/pagination (that
computation — `LIMIT pageSize + 1`, `hasMore = rows.size > pageSize` — is correct and unchanged),
it's that **`PassDao.getFilteredForSatellite` (the local Room path used once
`HistoryLoadState.isFullyLoaded` is true) was missing a filter the backend enforces
unconditionally**. The backend's `PassRepository.GetHistoryAsync` (`backend/src/
SatelliteTracker.Database/Repositories/PassRepository.cs`) always applies
`p.Los < DateTime.UtcNow` to every history query, regardless of which optional filters
(`aosFrom`/`aosTo`/`maxElevationFrom`/etc.) the caller supplied — "history" means "already
completed," unconditionally, on the backend. `getFilteredForSatellite`'s doc comment claimed it
"mirrors the backend's own filter semantics," and it did mirror the *optional* ones
(`aosFrom`/`aosTo`/`maxElevationFrom`), but not this always-on one — there was no
`losEpochMillis < now` clause in the local SQL at all.

This stayed invisible for satellites still being served over the network (correctly bounded by the
backend on every request) and only became visible once a satellite's `HistoryLoadState
.isFullyLoaded` flips `true`, switching `getPassHistory` to the local, unbounded-by-chronology Room
path. A satellite reaches `isFullyLoaded = true` sooner the fewer total historical passes it has to
paginate through — RUNNER-1, with a much smaller historical dataset than EROS C3, converges to
`hasMore = false` within very few pages (plausibly the first), while EROS C3's larger dataset keeps
it on the (correctly-bounded) network path for longer. Once RUNNER-1's local path activated, every
one of its still-upcoming passes — already sitting in the same `passes` Room table via the
ordinary `getPasses()` write path used for the Dashboard/Upcoming view — also satisfied the
(chronology-blind) local query and leaked into "History" results, making the two segments look
identical for that satellite specifically.

Fixed by adding the missing bound: `PassDao.getFilteredForSatellite` gained a `nowMillis: Long`
parameter and an unconditional `AND losEpochMillis < :nowMillis` clause (not one of the `:x IS
NULL OR ...` optional clauses — this one is never skippable, matching the backend). `PassRepository
.getPassHistory` passes its own already-computed `now` through as `nowMillis`. Covered by a new
`PassRepositoryHistoryTest` case asserting the DAO is called with exactly the repository's clock-
derived `now`, plus a dedicated page-boundary regression test (exactly one page of rows resolves
`hasMore = false`) for the (a) hypothesis this task asked to check explicitly, even though it
turned out not to be the actual cause. Verifying the SQL predicate itself end-to-end would need a
real (in-memory) Room database — this project has no Room-backed DAO test (instrumented or
otherwise) for `PassDao` at all yet, only repository-level tests that mock the DAO; flagged rather
than invented ad hoc for this round.

### 6. Elapsed-ratio ring — fully removed, not deferred as a placeholder

Per confirmed product decision: the empty static ring in `DashboardScreen`'s `HeroPassCard` is
removed outright (not kept as unfinished chrome) — a genuinely new, static (non-percentage) visual
replacement is planned via Claude Design in a future polish pass once other functional work is
complete; a computed "since previous LOS" percentage was explicitly considered and rejected as too
complex for the value it adds right now. A `TODO(design)` comment at the removal site in
`HeroPassCard` records this. The hero card's content `Row` now holds only the countdown/label/chip
`Column`, full width — the simplest layout without a ring element, per this task's instruction.

**Resolved (separate later task, "Static decorative animation for Next Pass Card"):** the
`TODO(design)` above is closed out. `HeroPassCard`'s leading visual slot is filled again, this time
by `PassArcAnimation` (private composable, same file) — a purely decorative, continuously looping
animation with **no backing data at all**: it does not read `nextPassCountdown`, does not compute
an elapsed fraction or any other ratio from `Pass`/`DashboardUiState`, and was not given a new
ViewModel/UiState field to support it. The "since previous LOS" computed-percentage idea from the
paragraph above remains explicitly rejected, not merely deferred a second time — this is not that.

Sourced 1:1 from the design MCP (Claude Design project "Map detail and AR improvements",
`SatelliteTracker M3.dc.html`, the 2a/M3-baseline Home screen's "M3 elevated card: countdown"
block): a small SVG animation of a dot endlessly tracing a stylized elevation-arc trajectory over
faint horizon/elevation-dome guide arcs (`stroke-dashoffset` self-drawing line + two
`animateMotion` dots, 4.8s loop). Reproduced in Compose via `Canvas` + `PathMeasure` — a
`rememberInfiniteTransition().animateFloat` progress value drives `PathMeasure.getSegment`/
`.getPosition` each frame to redraw the traced sub-path and the moving dot at the same point along
a `Path` built from the design's own curve. All four colors used (guide arcs, dim track, bright
trace, dot) map exactly onto existing tokens — `outlineVariant`/`primaryContainer`/`primary`/
`onPrimaryContainer` — no new color was introduced. The 104×104 SVG viewBox coordinates are used
as-is inside a `scale()` draw transform rather than converted to fixed dp values, so the whole
trajectory scales uniformly with the Canvas's actual size; that Canvas itself is sized 88dp,
matching the box size the pre-removal ring placeholder used (the design's own 390×844 mock frame
uses a larger scale than this app's actual card proportions call for).

Isolated into its own composable specifically so the animated `progress` value is only ever read
inside `Canvas`'s draw-phase lambda (a `DrawScope.() -> Unit` invoked during drawing, not
recomposition) rather than in `HeroPassCard`'s own body — the animation ticking therefore triggers
only a re-draw of this one small `Canvas` every frame, never a recomposition of `HeroPassCard`,
the countdown text, or `MetricGrid`. `rememberInfiniteTransition`'s animation coroutine is scoped
to `PassArcAnimation`'s own composition and is cancelled automatically once it leaves composition
(switching to a tab with no next pass, or navigating off Dashboard) — nothing extra was needed to
make this lifecycle-safe.

No test was added or changed — this has no backing state for a test to assert on, per the task's
own scope. Verified in this environment: `:app:compileDebugKotlin`, `:app:testDebugUnitTest` (156
tests green, unchanged from before this task), and `:app:assembleDebug`, all `BUILD SUCCESSFUL`.

### Testing summary for this round

Verified in this environment: `:app:compileDebugKotlin`, `:app:testDebugUnitTest` (**156 tests
green** — 151 before this round, +5 here: 3 in `PassDetailsViewModelTest` for `isHistorical`/
disabled-toggle behavior, 2 in `PassRepositoryHistoryTest` for the `nowMillis` bound and the page-
boundary case), and `:app:assembleDebug`, all `BUILD SUCCESSFUL`. **Not verified**:
`:app:connectedDebugAndroidTest` — no `adb`/connected device or emulator was available in this
environment, same limitation noted in every prior UI task. Two gaps are explicitly flagged rather
than silently skipped, per this round's own instructions: the navigation-debounce fix (item 3) and
the scroll-reset fix (item 4) both need testing infrastructure (Robolectric/`navigation-testing`,
and Compose UI testing respectively) that doesn't exist in this project yet.

---

## Milestone E, Step 5 — FCM push notifications (client side)

Token capture, the pending-token pattern, token sync, the notification-tap deep link, and the
permission request trigger. Relies on `app/google-services.json` (**gitignored, never
committed** — a checkout without it fails the build at `process*GoogleServices`, which is intended)
and on the backend's Notification+Data payload (`passId`, `type = "pass_reminder"` — repo-root
CLAUDE.md's FCM payload section).

### Firebase setup, and one dependency side effect to know about

- `firebase-bom` 34.19.0 + `firebase-messaging` (the BoM has not shipped `-ktx` modules since
  34.0.0; their APIs live in the main artifacts) and the `com.google.gms.google-services` plugin
  4.4.4 (the 4.4.x line, matching the AGP 8 pin — not 4.5.0). Kotlin stdlib resolves to 2.1.21,
  so the Kotlin 2.1.20 compiler pin is unaffected.
- **The BoM also bumps `androidx.datastore` from the pinned 1.1.1 to 1.1.7.** DataStore's
  file-based storage in 1.1.7 commits each write with `File.renameTo`, which cannot replace an
  existing file **on Windows**, so every JVM DataStore test broke on this dev machine
  (`"Unable to rename ... multiple instances of DataStore"`). Production is unaffected, since
  Android's rename does overwrite. Fixed only in tests: `testPreferencesDataStore(file)`
  (`src/test/.../data/local/TestPreferencesDataStore.kt`) builds the test DataStore on
  `OkioStorage`, whose atomic move replaces the target. Every DataStore-backed store test must
  use this helper, not `PreferenceDataStoreFactory.create(produceFile = ...)`.

### `FcmTokenStore` — the pending-token pattern

FCM can issue a token at any time, including before the tester has registered, when
`SessionManager` is `RequiresReauth` and `PUT /api/settings/me/fcm-token` would just return 401.
If that token were dropped, the backend wouldn't learn it until FCM happened to rotate it. So
every token goes into a single persisted **pending** slot unconditionally
(`FcmTokenStore.savePendingToken`, stored in the same Preferences DataStore file as
`HiddenSatellitesStore`, same interface + `DataStore*` implementation + `@Provides` binding
pattern). The slot is cleared only once the backend has confirmed the token.
`SatTrakkMessagingService.onNewToken` does no session check at all; it just saves on
`@ApplicationScope` (the service may be destroyed right after `onNewToken` returns).

### `FcmTokenSyncObserver` — process-lifetime sync on the shared `@ApplicationScope`

`data/push/FcmTokenSyncObserver.kt`, started once from `SatTrakkApplication.onCreate`. There was
no existing app-startup hook, so this adds field injection into the `@HiltAndroidApp` class for
the first time. It runs on the **existing** `@ApplicationScope` `CoroutineScope`
(`di/CoroutineScopeModule.kt`, the same scope `PassRepository.getPassById`'s background refresh
uses). No second scope was added.

- `combine(sessionState, pendingToken)` → `distinctUntilChanged` → `collectLatest`: when the state
  is `Valid` and a token is pending, it calls `SettingsRepository.updateFcmToken(token)`.
  `RequiresReauth` with a token pending does nothing, and the token waits.
- It clears the slot **only** on `ApiResult.Success`, and only if the slot still holds the token
  that was sent. A newer token saved mid-PUT is never dropped, and `collectLatest` also cancels
  the stale send.
- **Any failure leaves the token pending. There is no timed retry loop.** The retry happens on
  the next trigger: the next app launch (the first emission re-reads both values), a new token
  arriving, or the session flipping `RequiresReauth` → `Valid`.
- **Addition beyond the original spec:** on a `RequiresReauth` → `Valid` *transition* (a
  (re-)registration), the observer also calls `FcmTokenFetcher.fetchToken()`. Without it, a
  tester who re-registers gets a new `ApiKey` whose `UserSettings` row has no token, while the
  token synced under the old key was already cleared from the slot. The backend would never learn
  it until the next FCM rotation. This does not fire when the app *starts* already `Valid`.

### `FcmTokenFetcher` — proactive token fetch

`onNewToken` fires only when a token is created or rotated, never on an ordinary start. So
`FirebaseFcmTokenFetcher.fetchToken()` (`FirebaseMessaging.getInstance().token` → pending slot,
fire-and-forget on `@ApplicationScope`) is called:

- after the permission is granted (or found already granted) by Dashboard's one-time ask (below);
- on the re-registration transition above.

It sits behind an interface only so ViewModels and the observer can be unit-tested without
FirebaseMessaging's static singleton.

### Foreground vs. background display — `SatTrakkMessagingService.onMessageReceived`

Our pushes are Notification+Data hybrids. Per FCM's **documented** delivery rules:

- **App backgrounded or killed:** FCM shows the `Notification` block itself, and
  `onMessageReceived` is **not** called. On tap it launches the launcher Activity with every Data
  key copied in as a String extra.
- **App in the foreground:** nothing is shown automatically, and `onMessageReceived` **is**
  called.

So `onMessageReceived` builds an equivalent `NotificationCompat` notification by hand. It uses the
same title/body and puts the same `passId`/`type` extras on a `PendingIntent` to `MainActivity`
(`FLAG_IMMUTABLE`, with a per-pass request code and notification id, so a later threshold for the
same pass replaces the earlier reminder instead of stacking). It skips posting if
`NotificationPermissionManager.isGranted()` is false, and ignores any payload that isn't a
well-formed `pass_reminder`.

Both paths use the `pass_reminders` channel. It is created in `SatTrakkApplication.onCreate` and
declared as FCM's `default_notification_channel_id` in the manifest, along with
`default_notification_icon` (`drawable/ic_stat_pass_reminder`, an original monochrome glyph).
**The foreground/background behavior above is FCM's documented contract. It was not observed on a
device in this task.** See the verification notes below.

### Deep link: notification tap → Pass Details

1. **Parsing.** `PassNotificationDeepLink.passIdFrom(intent)` returns the passId only when
   `type == "pass_reminder"` and `passId` parses as a UUID. Anything else returns `null`,
   including an ordinary launcher tap or a future unknown `type`, so a bad payload can never build
   a garbage route. Both tap paths deliver the same extras (see above), so there is one parser.
2. **Intake.** `MainActivity` (now `launchMode="singleTop"`) passes its Intent to
   `AppViewModel.onLaunchIntent` in `onCreate` and in `onNewIntent` (tap while already running).
   In `onCreate` this happens only when `savedInstanceState == null`: on a recreation,
   `getIntent()` is still the original launch Intent, and the restored NavController back stack
   already reflects the link having been handled. `MainActivity` gets `AppViewModel` via
   `by viewModels()`, the same Activity-scoped instance `SatTrakkApp`'s `hiltViewModel()`
   resolves, so the pending link survives a config change. A non-reminder Intent never clears an
   unconsumed pending link.
3. **Navigation.** `SatTrakkApp` passes `pendingPassDetailsId` into `MainNavHost`. A
   `LaunchedEffect`, placed after `NavHost` so the graph is already set, calls the existing
   `navigateDebounced(PassDetails.buildRoute(passId))` (`launchSingleTop`, same as the row taps)
   and then `onPendingPassDetailsConsumed()`.
4. **`RequiresReauth` edge case — chosen: defer, don't drop.** Under `RequiresReauth`,
   `SatTrakkApp` doesn't compose `MainNavHost` at all, so there's no NavController to navigate
   and nothing can crash. The passId simply stays in `AppViewModel`. When the tester
   re-registers, the session flips to `Valid`, `MainNavHost` composes, and the `LaunchedEffect`
   consumes the link. If the process dies before then, the link is lost. That's an accepted
   tradeoff: the pass is still reachable from Dashboard.

### Where the soft-ask → hard-ask trigger actually lives (corrects the original plan)

The task plan assumed Dashboard already had a soft-ask UI. **It didn't.** The only permission UI
was the Settings screen's `PermissionStatusCard`, and Dashboard never surfaced permission status
at all. No Dashboard soft-ask UI was built here. The trigger is a one-time **direct system
dialog** on Dashboard:

- `DashboardViewModel.maybeRequestNotificationPermission()` runs right after the first successful
  `getSatellites()` load, if `NotificationPromptStore.hasRequestedNotificationPermission` is
  false. It marks the flag *before* anything else (persisted in DataStore, so it is never
  re-asked, even if the process dies mid-dialog). Then:
  - if `NotificationPermissionManager.isGranted()` (always true below API 33, or already granted
    from Settings), it calls `FcmTokenFetcher.fetchToken()` directly with no dialog;
  - otherwise it sets `requestNotificationPermission: StateFlow<Boolean>`.
- `DashboardScreen` launches the dialog with the **same mechanism SettingsScreen uses**:
  `rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission())`. It clears the
  flag (`onNotificationPermissionRequestLaunched()`) before launching, so a recomposition or
  config change can't launch it twice. The result goes to `onNotificationPermissionResult`, which
  calls `fetchToken()` on grant and does nothing on denial.
- After this one ask, the Settings screen's permission card (unchanged) is the only way back to
  the dialog or to system settings.

### What's automatically tested vs. manually verified only

Automatically tested (JVM unit tests, **192 green**: 156 before this task + 36 new):
`FcmTokenStoreTest` (5), `NotificationPromptStoreTest` (2), `FcmTokenSyncObserverTest` (10: send
+ clear on success, keep on failure/error, no send under `RequiresReauth`, deferred send on
`Valid`, the in-flight newer-token race, and the re-registration token fetch),
`PassNotificationDeepLinkTest` (9: extra parsing including malformed/unknown-type cases, plus the
route string, with `Uri.encode` mocked via `mockkStatic`), `AppViewModelTest` (4: pending-link
state), and 6 new `DashboardViewModelTest` cases for the permission trigger. Also
`:app:compileDebugKotlin` and `:app:assembleDebug` (manifest merge confirmed: service,
`singleTop`, default channel).

**Not automatically verified. All of these need a real device or emulator with Play Services, and
none was available:**
- Real token issuance and `onNewToken` firing; the token actually reaching the backend.
- Foreground vs. background display of the hybrid payload (the documented behavior above).
- A tray-notification tap delivering the extras, and the actual navigate into Pass Details, from
  both the cold-start and `onNewIntent` paths. The `LaunchedEffect` → `NavController` step can't
  be exercised without Compose UI test / Robolectric / `navigation-testing` infrastructure, the
  same gap noted in every prior UI task.
- The system permission dialog and its grant/deny callbacks on API 33+.
- `:app:connectedDebugAndroidTest` (no `adb` or emulator in this environment). Note that
  `MainActivityTest` now also initializes Firebase at app startup via `google-services.json`.

Suggested manual QA on a device: fresh install → register → accept the dialog → confirm the
`UserSettings.FcmToken` row on the backend. Then send a reminder with the app foregrounded, with
it backgrounded, and with it killed, and tap each one to confirm Pass Details opens for the right
pass. Also force `RequiresReauth` (deactivate the key), tap a reminder, re-register, and confirm
the modal opens afterward.

**Update:** this manual QA was run on an emulator during the post-merge integration QA at the end
of this file. Everything passed except background/killed **delivery**, which the environment
couldn't verify (the tap handling for those states did pass).

---

## Map screen — data layer + ViewModel/UiState (Milestone F prep, no UI yet)

`MapRepository`, `GeoUtils`, `MapViewModel`, and `MapUiState`. `MapScreen.kt` is still the
placeholder; the Composable is a separate task built on top of this. Covered by
`MapRepositoryTest` (5), `GeoUtilsTest` (6), and `MapViewModelTest` (12).

### `MapRepository` — no Room caching, by design

- `getPosition(satelliteId)` → `GET /api/satellites/{id}/position`, and `getLiveTrack(satelliteId)`
  → `GET /api/satellites/{id}/track`. Both are straight `safeApiCall` passthroughs with **no Room
  caching and no DAO writes**, for the same reason as `PassRepository.getPassTrack`: the backend
  already caches them (30 s and 5 min — repo-root CLAUDE.md's caching table), and real-time
  position data is never stored (repo-root "What NOT to do"). A client cache would add staleness,
  not value. `MapRepositoryTest` asserts the DAO is never touched on these paths.
- `getNotifyEnabledPasses()` → new `PassDao.getNotifyEnabled()` (`notify = 1`, every satellite,
  `ORDER BY aosEpochMillis ASC`). This is a local-only read, since `notify` is client-cached state.
  It backs the drawer in **both** flows.
- The fixed per-pass track is **not** duplicated here. `MapViewModel` reuses the existing
  `PassRepository.getPassTrack`.
- Timestamps: `PositionDto`/`TrackPointDto`/`PassTrackPointDto` all carry Unix **seconds** from the
  backend. `RealTimeMappers.kt` converts them to the domain's `timestampEpochMillis`. This task
  also fixed `PassTrackMappers`, which had been storing seconds in that millis field; it had no
  consumer before the Map screen.

### `GeoUtils` — footprint polygon (`domain/util/GeoUtils.kt`)

Pure spherical geometry, with no I/O and no Android dependency. The Earth is modelled as a sphere
of mean radius 6371 km. `footprintPolygon(center, radiusKm = 2000, points = 72)` returns an open
polygon (the first point is not repeated) of evenly spaced bearings θ. It uses the standard
great-circle destination-point formula (Ed Williams' Aviation Formulary / Movable Type), with
angular distance δ = r / R:

    φ2 = asin(sin φ1 · cos δ + cos φ1 · sin δ · cos θ)
    λ2 = λ1 + atan2(sin θ · sin δ · cos φ1, cos δ − sin φ1 · sin φ2)

Longitudes are normalized to [-180, 180). `distanceKm` (haversine) is the inverse check used by
`GeoUtilsTest`: every output point lies within 0.01 km of the requested radius, including near a
pole and across the antimeridian. Splitting a polygon that crosses the antimeridian for rendering
is left to the Map UI; this layer only produces correct coordinates.

### `MapViewModel` — two flows, chosen once from nav args

Both nav args are read from `SavedStateHandle` as **optional** (`passId`, `satelliteId`;
`MapViewModel.PASS_ID_ARG` / `SATELLITE_ID_ARG`):

- **`passId` present → `MapUiState.StaticPassTrack`.** `PassTrackDto` carries only `passId` +
  points (no AOS/LOS/elevation), so the header's `Pass` comes from the existing Room-first
  `PassRepository.getPassById`, fetched in parallel with `getPassTrack` (and, since the Map UI
  follow-up, the satellite catalog for the drawer's names; its failure is tolerated, see the Map
  UI section's Drawer part). It is one-shot: **no
  polling and no footprint** (confirmed decision). A failure in either call becomes `Error`.
- **`passId` absent → `MapUiState.LiveTrack`.** It loads the satellite catalog (for the display
  name, the same lookup `PassDetailsViewModel` uses rather than N2YO's `satName`), position, live
  track, and drawer in parallel, and computes the footprint from the position. Any failure in
  this initial load, or a `satelliteId` that isn't in the catalog, becomes `Error`.
- **Polling (live flow only)** runs as two independent `viewModelScope` loops, so both are
  cancelled when the ViewModel is cleared:
  - **Position every 15 s**, recomputing the footprint each time.
  - **Live track every 5 min**, not every 15 s. That matches its server-side cache TTL: polling
    faster only re-reads the backend's cached copy. The track covers about 5 minutes of flight
    (`RealTimeController`'s `seconds: 300`), so fetching it only once per visit would leave it
    visibly behind the moving marker during a long viewing session.
- **Poll failures keep the last good `LiveTrack`** and retry on the next tick. Only the *initial*
  load's failure becomes `Error`. This deliberately narrows the spec's "any failure → Error" so a
  transient blip doesn't blank a map the user is watching. An auth failure still flips
  `SessionManager` via `SafeApiCaller`, which replaces the whole app with Tester Entry.
- **The drawer (`notifyEnabledPasses`) loads in both flows.** It is read once at load time and
  not refreshed by polling.

### Open items, flagged rather than decided

- ~~**Map's route has no `satelliteId` yet.**~~ **Resolved** by the Map UI task: the route is now
  `map?passId=&satelliteId=`, and every entry point passes one (see "Map screen — MapLibre Compose
  UI + navigation" below). The FAB and bottom-nav Map item pass the Dashboard's
  `selectedSatellite`.
- **The drawer query is only as good as the local `notify` value.** `PassRepository` still
  defaults a first-seen pass to `notify = true` (in `getPasses`, `getPassById` and
  `getPassHistory`) from the old opt-out era, while the backend is now opt-in. So
  `getNotifyEnabled()` will return nearly every cached pass, not just the ones the tester turned
  on. The query also has no time bound, so past passes are included. Both need a decision before
  the drawer UI ships. Not changed here, because it alters existing repository behavior outside
  this task's scope.

### Testing

`MapViewModelTest` follows `DashboardViewModelTest`'s pattern: no `runTest`, and the Main
`TestDispatcher`'s scheduler is driven directly. Unlike that test, it **does** cover cancellation.
The ViewModel is created through a real `ViewModelStore` + `ViewModelProvider`, and the public
`ViewModelStore.clear()` runs `onCleared()` and cancels `viewModelScope`. It asserts no further
position or track calls after clearing. Also covered:
- the exact 15 s / 5 min poll boundaries;
- zero live-endpoint calls in the static flow over simulated time;
- keep-last-good on a failed poll;
- initial-failure and missing-arg `Error`s.

Verified in this environment: `:app:testDebugUnitTest` (**215 green**: 192 before + 23 new) and
`:app:assembleDebug`. Not verified: `:app:connectedDebugAndroidTest` (no device or emulator).

---

## Map screen — MapLibre Compose UI + navigation (Milestone F)

Replaces the `MapScreen.kt` placeholder with real content, built on the Map data layer above
(`MapViewModel`/`MapUiState`, consumed **exactly as built**, with no ViewModel/repository changes),
plus the nav-graph changes for the two flows. Branch `feature/map-screen-ui`, cut from `develop`
after PR #42 (map data layer) merged. Does not touch any other screen beyond the nav wiring below.

### Code truth map re-verification (Screen 5/8 — Map)

`CodeTruthMap.md`'s Map entries were written under the old "no MapViewModel, osmdroid placeholder"
assumption. Re-verified item by item against the current code, not carried over wholesale:

- **[REAL] Basemap**: inline style over CartoDB Dark Matter raster tiles (below).
- **[REAL] Ground-track polyline (dashed)**: `LiveTrack.trackPoints` (Flow 1, polled every 5 min)
  or `StaticPassTrack.trackPoints` (Flow 2, solid line, framed by its bounding box).
- **[REAL] Live position dot**: `LiveTrack.currentPosition`, polled every 15 s. It's a static
  halo + dot, not an animated pulse; the pulse animation is still decorative and not built.
- **[REAL] Footprint polygon**: `LiveTrack.footprintPolygon` (Flow 1 only; none in Flow 2, per the
  confirmed decision).
- **[REAL] Satellite-name floating label**: `LiveTrack.satelliteName`, anchored above the marker.
- **[REAL] Back arrow + top bar**: nav-only. The title is the satellite name (Flow 1) or "Pass track"
  + orbit/AOS subtitle (Flow 2).
- **[DECORATIVE, still omitted]** Layer/zoom/compass FABs (no map-control state; pinch-zoom works
  natively), the bottom-sheet lat/lon/altitude/velocity readout (`SatellitePosition` has no
  velocity, and a readout panel wasn't in scope), and the "Over Israel" badge (no geofence exists).
- **No 2D/3D globe toggle.** MapLibre Native has no globe projection on mobile. Camera tilt/pitch
  is out of scope, since nothing in the truth map calls for it.

### MapLibre Compose + the inline CartoDB style

- **`org.maplibre.compose:maplibre-compose` 0.12.1**, pinned below the newest release (0.17.0)
  for the same reason as the kotlinx.serialization pin: 0.13.0+ are built against kotlin-stdlib
  2.3.x/2.4.x and Compose 1.10+/1.12, which the Kotlin 2.1.20 compiler can't read. 0.12.1 is the
  newest on kotlin-stdlib 2.2.x (one version ahead, which 2.1.20 can still read) and Compose 1.9.x.
  Checked per release via each version's Gradle `.module` metadata on Maven Central. Bump it
  together with `kotlin`. The library calls `MapLibre.getInstance(context)` itself (no app-side
  init). The APK is now ~90 MB because MapLibre ships native libs for every ABI; consider ABI splits
  or an App Bundle before a real release.
- **Inline style, not a hosted one** (confirmed decision): `CARTO_DARK_STYLE` in `MapScreen.kt` is a
  `BaseStyle.Json` with one raster source and one raster layer. It uses the same CartoDB Dark Matter
  tiles the web frontend uses (`frontend/src/components/SatelliteMap.tsx`), with no API key.
  Leaflet's `{s}`/`{r}` placeholders don't exist in MapLibre, so the four `a`–`d` subdomains are
  listed explicitly, and the `@2x` retina tiles are requested at `tileSize: 256`. The source
  carries the "© OpenStreetMap contributors © CARTO" attribution. The attribution/logo ornaments
  stay on (tile terms); the scale bar and compass are off.
- **No glyphs, so no map-rendered text.** The style references nothing but the tile source, so a
  MapLibre `SymbolLayer` can't draw labels. The satellite-name label is a Compose overlay instead,
  positioned through `CameraState.projection.screenLocationFromPosition`. It lives in its own small
  composable (`SatelliteLabel`), so the per-frame camera reads while panning only recompose the
  label.
- **Layers** (bottom to top, Flow 1): footprint fill (12% `primary`) + outline, dashed track,
  position halo + dot. Flow 2: track + AOS (filled) / LOS (hollow) end markers. The AOS/LOS
  markers were optional per the task; they're kept because without them the track has no
  direction. Colors come from existing theme tokens (`primary`/`onPrimary`/`background`), read in
  normal composition and passed in as plain values.
- **Camera**: Flow 1 centers once on the position at first display (zoom 2.5, which fits the
  ~4000 km footprint). Later polls move the marker but not the camera, since the user may have
  panned. Flow 2 animates to the track's bounding box (48 dp padding), except when the track is
  split at the antimeridian: a bbox across ±180° would span the world, so it stays centered on AOS.

### `MapGeometry` — antimeridian/polar rendering (`ui/map/MapGeometry.kt`)

`GeoUtils` (the domain layer) deliberately left antimeridian handling to the renderer. This is that
half, pure and JVM-tested (`MapGeometryTest`, 10 cases):

- `splitAtAntimeridian(points)`: splits a track wherever consecutive longitudes jump by more than
  180°. It interpolates the crossing latitude and gives each side an explicit ±180° endpoint, so
  the pieces meet exactly. The result is rendered as a `MultiLineString`.
- `footprintRing(ring, center)`: unwraps the footprint's longitudes to be continuous around the
  center, so near ±180° it draws as one intact circle on the adjacent world copy instead of
  splitting. If the ring encloses a pole (above roughly ±72° latitude), it finishes the circle to
  the first point's 360°-shifted copy and closes across the pole side at MapLibre's Mercator limit
  (±85.05°), which fills the polar cap.

### Navigation — optional `passId`/`satelliteId`, the guard, and every entry point

- **Route**: `map?passId={passId}&satelliteId={satelliteId}`. Both are `NavType.StringType`,
  `nullable = true`, `defaultValue = null`, keyed by `MapViewModel.PASS_ID_ARG`/`SATELLITE_ID_ARG`.
  `SatTrakkDestination.Map.buildRoute(passId, satelliteId)` follows the `Uri.encode` convention of
  `FullPassList`/`PassDetails`. **`satelliteId` goes beyond the task's `map?passId={passId}`
  sketch**: `MapViewModel`'s live flow needs it, and without it shows "No satellite selected."
- **Entry points**:
  - Dashboard FAB → Flow 1 for the Dashboard's `selectedSatellite`.
  - Bottom-nav Map item → Flow 1 for `selectedSatellite`. It is disabled until one is known, the
    same fallback the Passes item uses. It uses `navigateToTopLevel(..., restoreState = false)`,
    so a previously visited Map entry (possibly a pass track) is never restored; the tab is always
    "live, now."
  - Pass Details' "Show on map" → pops the modal, then Flow 2 for that `passId`.
  - Map drawer tap → Flow 2 for the tapped `passId`.
- **The guard (`navigateToMap`)**: `popUpTo(Map.route) { inclusive = true }` + `launchSingleTop`.
  At most one Map entry ever exists, drawer-hopping never accumulates entries, and back always
  leaves Map in one step. Confirmed in `NavControllerImpl.navigate` (navigation-runtime 2.9.7):
  `popUpTo` runs **before** the single-top check. That is exactly why `launchSingleTop` alone would
  have been wrong here: it would reuse the current Map entry, whose `MapViewModel` reads its args
  only once in `init`, so a drawer tap would silently keep showing the old pass. With the pop
  first, `launchSingleTop` can never match, so it's kept only for parity with `navigateDebounced`.
- **Rapid-tap debounce**: the same synchronous "is the target already the current top entry?"
  check that `launchSingleTop` makes, but args-aware (`isShowingMap(passId, satelliteId)`). A
  repeat tap on the same drawer entry, FAB, or Map tab is dropped instead of tearing down and
  recreating the screen. Like `navigateDebounced`, it isn't covered by an automated test (no
  Robolectric/`navigation-testing`/Compose UI test infrastructure).
- **Leaving Map via the bottom bar no longer saves its state.** `navigateToTopLevel` pops with
  `saveState = true`, which keeps a popped entry's `ViewModelStore` alive for a later restore. For
  Map that would have kept `MapViewModel`'s `viewModelScope` polling (position every 15 s) running
  in the background after the user left. `navigateToTopLevel` now passes `saveState = false` when
  the current destination is Map. Other tabs are unaffected.

### Drawer — available in both flows

A `ModalNavigationDrawer` listing `MapUiState.notifyEnabledPasses` (both `LiveTrack` and
`StaticPassTrack` carry it). It opens from the top bar's list button in both flows. Swipe-to-open
is disabled (`gesturesEnabled = drawerState.isOpen`) because it fights the map's pan gesture;
swiping or tapping the scrim still closes it. Loading and Error states show an empty-state line.
Each row shows the satellite name, the AOS local date/time, and "Orbit N", using existing `Pass`
fields only. In Flow 2 the row for the pass on screen is highlighted. A tap closes the drawer and
goes through `navigateToMap(passId = ...)`.

**Satellite names — resolved by a `MapViewModel` catalog lookup (follow-up to the UI task):**
`Pass` carries only `satelliteId`, and the drawer lists passes of *every* satellite. So both
`MapUiState.LiveTrack` and `.StaticPassTrack` now carry `satelliteNames: Map<String, String>`
(the whole catalog, id → name), built from `SatelliteRepository.getSatellites()` (24h TTL, Room-
cached). This is the same "fetch the catalog, match by id" pattern `PassDetailsViewModel` uses,
with no new repository method.
- **Flow 1** reuses the catalog it already fetched for `satelliteName`, so there is still exactly
  one `getSatellites()` call.
- **Flow 2** adds `getSatellites()` to its existing parallel `async` batch. A failure there is
  **tolerated**: `satelliteNames` is left empty and the pass track still renders, while pass and
  track failures remain whole-screen `Error`s. Names are secondary content, the same asymmetry as
  Pass Details' partial-content handling.
- The drawer shows `satelliteNames[pass.satelliteId]`, falling back to "Unknown satellite" only
  when that lookup failed or the id isn't in the catalog.
- Covered by `MapViewModelTest`: names across satellites in both flows, a single catalog call in
  Flow 1, and a Flow 2 catalog failure still yielding `StaticPassTrack`.

**Flagged, not resolved:**
- **Drawer contents**: still subject to the data layer's open item above. First-seen passes
  default to `notify = true` locally while the backend is opt-in, and the query has no time bound,
  so the drawer will list most cached passes, including past ones.

### Testing

- No Compose UI test convention exists in this project beyond the coarse `MainActivityTest`, and
  none was invented here. Same flag as every prior UI task. The map rendering, the drawer, the
  label anchoring, and the nav guard (real `NavController` behavior) are therefore **not**
  automatically tested. `MapViewModel` itself was already covered by `MapViewModelTest`.
- New: `MapGeometryTest` (10), plus 2 `MapViewModelTest` cases for the satellite-name lookup
  (12 → 14).
- Verified in this environment: `:app:compileDebugKotlin`, `:app:testDebugUnitTest` (**227
  green**: 215 before + 10 + 2), and `:app:assembleDebug`, all `BUILD SUCCESSFUL`.
- **Not verified**: `:app:connectedDebugAndroidTest` and any on-device rendering (no `adb` or
  emulator available here). Tiles actually loading, the GL surface rendering inside Compose, and
  label placement need a device.
- Suggested QA:
  1. Dashboard FAB → live map: tiles, footprint, and marker appear, and the marker moves after about 15 s.
  2. Open the drawer → tap a pass → the pass track is framed.
  3. Tap another pass → press back once → you land back on Dashboard.
  4. Pass Details "Show on map".
  5. Bottom-nav Map, then another tab: confirm in the backend logs that position polling stops.
- **All five suggested QA steps above were run on an emulator in the post-merge integration QA
  below and passed.** That section supersedes this one's "Not verified: on-device rendering".

---

## Post-merge integration QA — FCM + Map (2026-09-24)

**Milestone E Step 5 (FCM) and Step 6 (Map) are complete.** Both are merged into `develop` (PRs
#41, #42, #43). This run was verification and reconciliation after all three merged, not new
feature work. **No app code was changed.** Every issue found was either an environment artifact
or is flagged below as a follow-up.

**Environment:**
- Real backend (`SatelliteTracker.API` on `:5076`) against the dev Postgres.
- `Pixel_10` AVD, API 36 Google APIs image with Play Services.
- Real FCM sends from `PassNotificationJob`, triggered by synthetic passes: a real EROS C3 pass
  cloned with AOS at now + 5.5 min, the QA tester opted in, `AlertMinutes = {5}`.
- All QA rows (passes, keys, settings, allowlist entry) were deleted afterwards.

### 1. Build and regression — PASS

- Clean `:app:compileDebugKotlin`, `:app:testDebugUnitTest` and `:app:assembleDebug`, all
  `BUILD SUCCESSFUL`.
- **227 tests, 0 failures, 0 skipped** across 28 suites. That's exactly 192 (FCM) + 23 (Map data
  layer) + 12 (Map UI), so no tests were lost in the merges.
- The merges didn't overwrite each other. `MapRepository`, `MapViewModel`, `GeoUtils`,
  `FcmTokenStore` and the four `data/push/` files are all present. `SatTrakkNavHost` carries both
  the Map `passId`/`satelliteId` args and FCM's deep-link `LaunchedEffect`. The Map PR's
  `saveState = false` change in `navigateToTopLevel` doesn't touch FCM's `navigateDebounced`.

### 2. FCM checklist

| Item | Result | How |
|---|---|---|
| Token capture before login | **PASS** | Fresh install, not registered: `pending_fcm_token` held a real `…:APA91b…` token in DataStore |
| Token sync on registration | **PASS** | Registering cleared the pending slot; `UserSettings.FcmToken` in the DB matched the device token |
| Permission flow | **PASS** | First Dashboard load showed the system dialog; `has_requested_notification_permission` was set; Allow → Settings shows "Push notifications enabled" |
| Delivery, foreground | **PASS** | Pushed while on the live Map: posted by `SatTrakkMessagingService` on `pass_reminders` with the right title/text |
| Delivery, background / killed | **NOT VERIFIED** (environment) | After the emulator's first reboot, it stopped receiving *any* FCM message, even in the foreground. The backend sent all three with no errors, the token was unchanged, the host could reach `mtalk.google.com:5228`. The emulator's Play Services session is the suspect. Needs a real device. |
| Tap-to-open, app running | **PASS** | A real tray tap opened Pass Details for the right `passId` over the Map (`onNewIntent`) |
| Tap-to-open, backgrounded | **PASS** (simulated Intent) | Sent the same launcher Intent + String extras FCM's tray notification sends. `singleTop` → `onNewIntent` → right pass |
| Tap-to-open, killed | **PASS** (simulated Intent) | Same Intent after `am force-stop`: cold start → Dashboard → Pass Details for the right pass |
| RequiresReauth edge case | **PASS** | Deactivated the key → an authorized call got 401 → Tester Entry. A deep link sent then didn't crash and didn't navigate. Re-registering opened the held link's Pass Details, and the token was synced to the **new** `ApiKey` within 0.5 s (the re-registration fetch) |

Anonymous GETs keep working with a deactivated key. The switch to Tester Entry needs an
`[Authorize]`d call, e.g. the notify toggle or Settings. That's the designed behavior, but it
matters when reproducing this case.

### 3. Map checklist

| Item | Result | Notes |
|---|---|---|
| Flow 1: live marker polls every 15 s | **PASS** | Marker/footprint visibly moved; the backend request log showed 4 position requests in ~50 s |
| Flow 1: footprint | **PASS** | Renders as a circle around the marker |
| Flow 1: track polyline | **PASS on host GPU** | Invisible under the emulator's default SwiftShader GPU. Removing `dasharray` made it appear, and the **unmodified** build draws it dashed with `-gpu host`. An emulator rendering artifact, not a code bug. |
| Flow 1: drawer opens, lists notify = true passes | **PASS, with caveat** | Opens and lists names across satellites. The contents are the open item below. |
| Flow 2: static track, no marker/footprint | **PASS** | Solid track with AOS/LOS markers, bbox-framed, drawer still reachable |
| Drawer hops, no back-stack buildup | **PASS** | Pass → another pass → double-tap on the same entry → one back press → Dashboard |
| Tiles load | **FAIL (external)** | Every CARTO tile is watermarked "API KEY REQUIRED", with any request headers; the web frontend is affected too. See follow-ups. |
| No crash on teardown while polling; polling stops | **PASS** | 0 position requests in 50 s after leaving via bottom nav, and 0 after leaving via back. No crash across many Map exits. |

### 4. Cross-feature checks

- **Push while on Map Flow 1 — PASS.** The notification posted, the Map kept polling, no crash.
- **Hiding the Map's satellite in Settings — works, but inconsistent (reported, not changed).**
  You can't be on Map and Settings at once, so the tested path was: Map (EROS C3) → Settings →
  hide EROS C3 → bottom-nav Map.
  - **It reopened EROS C3's live track anyway.** `MapViewModel` doesn't observe
    `HiddenSatellitesStore`, and the bottom-nav/FAB target is `MainNavHost`'s `selectedSatellite`.
    That only updates when `DashboardScreen` recomposes, so it goes stale while Dashboard is off
    screen.
  - Once Dashboard recomposed (it correctly dropped the EROS C3 tab), the next Map entry showed
    RUNNER-1.
  - The drawer keeps listing hidden satellites' passes.
  - No crash, and polling was unaffected.
  - The Dashboard hidden-satellite fix from round 1 does **not** extend to Map. Whether it should
    is a product call.

### Small fixes made

None. Every problem found is either an environment artifact (SwiftShader dashes, emulator FCM
delivery) or a behavior/product decision, listed below as a follow-up rather than changed here.

### Manual-only items (still not covered by automated tests)

- **Real FCM delivery to a backgrounded or killed app.** Unverified here; needs a physical device.
  The tap handling for both states *is* verified, via the equivalent Intent.
- Everything this run verified on the emulator stays manual-only: permission dialog, token
  issuance/sync, notification rendering and tap, Map rendering/polling/drawer/nav guard. The
  project still has no Compose UI test, Robolectric or `navigation-testing` infrastructure (the
  same gap as every earlier UI task).
- `:app:connectedDebugAndroidTest` was not run.

### Open follow-ups (flagged, not changed)

1. **Map basemap: CARTO keyless tiles are now watermarked.** Pick another dark raster provider,
   or get a CARTO key (client-side by nature). Affects the web frontend too.
2. **Drawer / `notify` default.** `PassRepository` still defaults first-seen passes to
   `notify = true` locally, while the backend is opt-in, so:
   - the drawer lists nearly every cached pass;
   - **Pass Details shows "Notify me" ON for passes the backend will never notify about.**
     Verified on device: a fresh tester, with no opt-ins except the synthetic one, saw every real
     pass in the drawer.

   Fixing it means flipping the default *and* repairing rows already cached on devices (the merge
   preserves cached values), which is more than a small fix. Separately, `getNotifyEnabled()` has
   no time bound, so past passes stay listed, and it doesn't respect hidden satellites.
3. **Map vs. hidden satellites.** See cross-feature check 4.
4. **Backend: a failed FCM send is logged as sent and never retried.** See the repo-root
   CLAUDE.md's "Android client status" section.
