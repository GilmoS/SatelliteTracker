# CLAUDE.md — SatTrakk Android

Android client (Kotlin, Jetpack Compose) for the Satellite Pass Tracker. See the repo-root
`CLAUDE.md` for the overall project/backend context — this file only covers what's specific to
the Android app. Talks only to `SatelliteTracker.API`; never calls N2YO or Microsoft Graph
directly (see repo-root CLAUDE.md, "Single Source of Truth Rules").

This file currently covers what's built as of Milestone E, Step 2.1 (networking + auth
infrastructure), Step 2.2 (Satellite/Pass/Notes repositories with Room caching), Step 2.3
(`AuthRepository`/`SettingsRepository`), Step 3.1 (`SessionManager` + `DashboardViewModel` +
`DashboardUiState` — logic only, no UI yet), and the Full Pass List screen's data layer
(`PassRepository.getPassHistory`, `HistoryLoadStateEntity`/Dao, `FullPassListViewModel` +
`FullPassListUiState` — logic only, no UI yet, see below). **Step 2 (the entire Android data
layer) is complete.** The rest of Step 3 (Dashboard Composable UI, Pass Details Modal, Settings
ViewModel, and a Composable for the Full Pass List screen) is next — this file will grow again
once those land.

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
│   │   ├── AppDatabase.kt, PassDao.kt, SatelliteDao.kt, NoteDao.kt, CacheMetadataDao.kt,
│   │   │                          HistoryLoadStateDao.kt (Full Pass List screen)
│   │   └── entity/                 Room @Entity classes (Pass, Satellite, Note, CacheMetadata,
│   │                                HistoryLoadState)
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
│   └── CoroutineScopeModule.kt     @ApplicationScope CoroutineScope, for fire-and-forget work outliving a caller
├── ui/
│   ├── dashboard/
│   │   ├── DashboardUiState.kt     SatelliteTabState + DashboardUiState (step 3.1)
│   │   └── DashboardViewModel.kt   Dashboard screen logic — no Composable yet (step 3.1)
│   └── fullpasslist/
│       ├── FullPassListUiState.kt  PassListFilter + FullPassListUiState (Full Pass List screen)
│       └── FullPassListViewModel.kt Full Pass List screen logic — no Composable yet
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
