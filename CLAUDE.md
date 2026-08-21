# CLAUDE.md — Satellite Pass Tracker

## Project Context
Satellite pass tracking system for Israel Aerospace Industries (IAI).
Tracks satellite passes over Israel, displays 2D/3D maps, integrates with calendar sync, and includes an Android app with AR Sky View mode.
Default satellites: EROS C3, RUNNER 1.
TLE data source: N2YO.com API.

---

## Commands

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run the API
dotnet run --project backend/src/SatelliteTracker.API

# Run all tests
dotnet test

# Run a single test project
dotnet test backend/src/<TestProject>/<TestProject>.csproj

# Run a single test by name
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Add a new EF Core migration
dotnet ef migrations add <MigrationName> --project backend/src/SatelliteTracker.Database

# Apply migrations
dotnet ef database update --project backend/src/SatelliteTracker.Database
```

Connection string: `ConnectionStrings:DefaultConnection` in `appsettings.Development.json` (not committed).

---

## Architecture — Modular Monolith

One .NET solution, five logical modules. Clients (Web + Android) talk ONLY to our backend.
The backend is the ONLY entity that communicates with N2YO and (in the future) Microsoft Graph API.

```
SatelliteTracker.API            → Controllers, Routing, Validation (thin layer)
SatelliteTracker.TLEService     → N2YO client, TLE parsing, scheduled fetch job
SatelliteTracker.PassService    → SGP4 pass calculation logic
SatelliteTracker.OutlookService → Calendar sync abstraction (ICS MVP; Graph stub for later)
SatelliteTracker.Database       → EF Core, Migrations, Repositories
```

### Single Source of Truth Rules
- Web and Android clients NEVER call N2YO or Microsoft Graph directly
- The N2YO API key lives ONLY on the backend — never exposed to clients
- All data flows through one point: our API

---

## Calendar Sync — ICS MVP (SatelliteTracker.OutlookService)

Microsoft Graph API calendar sync was replaced with an ICS-based MVP: no admin consent is
available for org calendar access on this unofficial project. `ICalendarSyncService` is the
abstraction boundary — swapping calendar backends is a single DI change with zero changes to
API/PassService/Controllers.

**Final flow:** the user selects one or more specific upcoming passes from the passes table
already shown in the app, chooses alert minute(s) for the request, and
`POST /api/calendar/schedule` (`CalendarController`, the real production entry point) returns a
single combined `.ics` for exactly those passes. The Android app opens it via `ACTION_VIEW` to
add it to the user's own calendar, and can `ACTION_SEND` it to share with a team lead — there is
no backend email-sending, no Microsoft Graph, and no push notification involved in this flow.
This replaced the originally planned Graph-based bulk date-range sync. The earlier
`DevCalendarTestController` manual-verification endpoint has been removed now that the real
controller exists.

- **`IcsCalendarSyncService`** is the active implementation, registered in
  `OutlookServiceCollectionExtensions.AddOutlookServiceModule()`. It builds one combined RFC 5545
  `.ics` document (one `VEVENT` per pass, `TZID=Asia/Jerusalem`). `CancelSyncedPassesAsync` throws
  `NotSupportedException` by design — the ICS model has no programmatic access to the user's
  calendar (events are added manually), and there is no product requirement to cancel a
  scheduled event from the app. This is a deliberate, permanent boundary, not a pending TODO.
- **`GraphCalendarSyncService`** is a stub (`NotImplementedException` on both methods) for future
  use if IT admin consent for Microsoft Graph is obtained. Swap it in by changing the single
  registration line in `OutlookServiceCollectionExtensions`.
- **UID formula:** `CalendarEventMapper` builds each event's `Uid` as
  `{norad_id}-{orbit_number}@sattrakk.com`. It was chosen over alternatives (e.g. a rounded-AOS
  timestamp) because it's stable and monotonic per satellite and unique per physical pass, so a
  re-sync updates the existing calendar event instead of creating a duplicate. Accepted limitation:
  a TLE re-snapshot can rarely shift `orbit_number` by one at an orbit-count boundary for the same
  physical pass, causing a duplicate event on re-sync instead of an update — this is deliberately
  not engineered around (low severity, user deletes the stray duplicate manually), not a pending TODO.
- **`TeamEmail`** on `CalendarSyncSettings` is intentional dead code / future-Graph
  infrastructure. `CalendarController` always passes `TeamEmail: null` — the ICS model has no
  programmatic way to notify a third party, so sharing happens on-device via `ACTION_SEND` after
  the user receives the `.ics`. Not wired to any current behavior.
- **`OutlookSynced`** semantics are weakened in the ICS MVP: it means "an `.ics` was generated and
  offered to the user," not "confirmed added to a calendar" (the ICS model has no way to confirm
  that). The field/name is kept as-is for forward compatibility with a future Graph
  implementation, where it would regain its original "confirmed synced" meaning.

---

## Caching Strategy (IMemoryCache)

| Endpoint         | TTL        | Reason                          |
|------------------|------------|---------------------------------|
| /position        | 30 seconds | Changes rapidly                 |
| /track           | 5 minutes  | Changes slowly                  |
| /passes          | 1 hour     | Pre-calculated, stable          |
| /tles            | 2 hours    | N2YO updates infrequently       |

No Redis needed at this scale. IMemoryCache is built into .NET.

---

## Database — PostgreSQL

Ten tables. All PKs are UUID (Guid in C#). All relationships via Fluent API in OnModelCreating.

| Table                  | Purpose                                                          |
|------------------------|-------------------------------------------------------------------|
| satellites             | Satellite catalog. NoradId is unique.                             |
| tles                   | TLE history per satellite. Up to 6 months back.                   |
| passes                 | Calculated passes. 1 week forward + 6 months history.             |
| notes                  | Free-text per pass. CASCADE delete with pass.                     |
| settings               | Single global row: MinElevation, OutlookDays, TeamEmail.          |
| api_keys               | One row per registered beta tester. See below.                    |
| user_settings          | Per-tester notification prefs, 1:1 with api_keys. See below.      |
| pass_subscriptions     | Per-tester notify opt-out per pass. Sparse. See below.            |
| pass_notification_logs | Per-tester, per-threshold sent-notification ledger. See below.    |
| allowlisted_emails     | Admin-managed beta signup allowlist. See below.                   |

### Beta multi-tester model — ApiKey and UserSettings

The app moved from a single global settings row to per-tester API keys for the beta:

- **`ApiKey`** — one row per registered tester (`Email`, `DisplayName`, `KeyHash`, `IsActive`,
  `CreatedAt`, `LastUsedAt`). The raw key is a random 256-bit value
  (`RandomNumberGenerator.GetBytes(32)`) shown to the tester exactly once at registration and
  never stored — only `KeyHash` (its SHA-256 hash, via `ApiKeyHasher` in
  `SatelliteTracker.Database.Security`) is persisted. `DisplayName` exists separately from
  `Email` so testers can be identified in logs/manual testing without exposing email everywhere.
  **SHA-256, not bcrypt/PBKDF2/Argon2, is intentional**: slow hashes exist to slow down
  brute-forcing low-entropy, human-chosen secrets (passwords). The raw key here is fully random
  at 256 bits, so guessing it is computationally infeasible even against a fast hash — a slow
  hash would only add cost to every request with no security benefit. Do not "fix" this later.
- **`UserSettings`** — per-tester `FcmToken` and `AlertMinutes` (the fields that used to live on
  the global `Settings` row), 1:1 with `ApiKey` via a required `ApiKeyId` FK. The 1:1 is enforced
  with `HasIndex(x => x.ApiKeyId).IsUnique()` in `OnModelCreating` — the navigation property alone
  does not enforce it.
- **`Settings.OutlookDays` and `Settings.TeamEmail`** remain in the schema but are **not read from
  anywhere in the logic yet** — they're prep for a future Microsoft Graph integration pending IT
  admin consent, not dead code to clean up. See the Calendar Sync section above for the current
  ICS-based flow that replaced the Graph-based design these fields were originally meant for.
- `PassNotificationJob` reads all active testers' `UserSettings` via
  `IUserSettingsRepository.GetAllActiveAsync()` (filtered to `ApiKey.IsActive`) and notifies each
  independently rather than reading one global FCM token/alert-minutes pair.

### Per-tester notification state — PassSubscription and PassNotificationLog

`Pass` used to carry `Notify`, `NotificationSent`, and `NotificationSentAt` — all three assumed a
single global recipient, which doesn't work once there are multiple independent testers. **These
three fields were removed from `Pass` entirely** (dropped in the `SplitPassNotificationState`
migration) — not deprecated, not kept-but-unused. That state is now split into two tables:

- **`PassSubscription`** — per-tester notify opt-out for a specific pass. **Sparse/opt-out
  model**: a row exists ONLY once a tester has actively toggled notifications off for that pass —
  there is no row created by default when a pass is calculated or when a tester registers.
  Absence of a row means `Notify = true`. Every read of this table (including in
  `PassNotificationJob`) must treat a missing row as `Notify = true` — `IPassSubscriptionRepository
  .GetEffectiveNotifyStatusAsync(passId, apiKeyId)` encodes this LEFT JOIN + COALESCE explicitly
  rather than exposing a generic `GetAsync` that could be misread as "null means unsubscribed."
  Unique index on `(PassId, ApiKeyId)`.
- **`PassNotificationLog`** — append-only ledger of notifications actually sent, one row per
  `(PassId, ApiKeyId, AlertMinutes)` threshold that fired, never a flag that gets flipped back. A
  single tester can have multiple rows for the same pass — one per `AlertMinutes` threshold (e.g.
  `[5, 10, 30]` can produce up to 3 rows for one pass, one per job tick that matches a threshold).
  Unique index on `(PassId, ApiKeyId, AlertMinutes)`. `IPassNotificationLogRepository
  .TryInsertAsync` catches the unique-constraint violation from a concurrent job tick and returns
  `false` ("already logged") instead of throwing — do not let that exception propagate.
- `PassNotificationJob` fetches all future passes, then for each (pass, active tester) pair checks
  `PassSubscription` (sparse opt-out) before checking `PassNotificationLog` per `AlertMinutes`
  threshold. This fixes the previous bug where a single global `NotificationSent` flag meant that
  once any tester's earliest threshold fired, the pass was marked done and no other tester —
  including one who registered afterward — could ever be notified about it.
- Both `PassSubscription` and `PassNotificationLog` rows CASCADE-delete when their `Pass` row is
  deleted (mirrors `Note`'s cascade pattern) — this covers the "pass cancelled/recalculated" case,
  since `PassService.CalculateAndSavePassesAsync` deletes and replaces upcoming `Pass` rows via
  `IPassRepository.DeleteUpcomingAsync` on every recalculation.
- **Unresolved tension, flagged for a decision, not resolved here**: there is no existing job that
  deletes `Pass` rows once they're in the past (LOS in the past) — the 6-month history requirement
  means historical `Pass` rows are kept, only filtered out by `GetHistoryAsync`'s date range, never
  deleted. So cascade delete never fires for "pass has passed." `IPassSubscriptionRepository
  .DeleteByPassIdAsync` and `IPassNotificationLogRepository.DeleteByPassIdAsync` exist as
  standalone primitives (delete the subscription/log rows without deleting the `Pass` row) for a
  future expiry job, but nothing calls them yet. Deciding whether/when to clean up
  `PassSubscription`/`PassNotificationLog` rows for expired-but-retained passes is a product
  decision, not made here.

`PATCH /api/passes/{id}/notify` is fully implemented (Milestone E, Step 1.3) —
`PassesController.PatchNotify` resolves the caller's `ApiKeyId` from the authenticated principal
(see Tester Authentication below) and upserts the tester's `PassSubscription` row via
`IPassSubscriptionRepository`. `Notify = true` is the sparse default, so setting it back to true
*deletes* any existing override row (via the new `DeleteOverrideAsync`) rather than writing a
redundant "true" row — `Notify = false` still goes through `SetNotifyAsync`. The endpoint requires
`[Authorize]` under the `ApiKey` scheme and returns the pass's effective notify status.

### Beta allowlist and self-registration — AllowlistedEmail, admin tooling, /api/auth/register

**⚠️ This entire mechanism is temporary beta infrastructure (Milestone E, Step 1.2). It is not a
foundation to build on** — production will eventually use proper org SSO, and if the allowlist
outgrows manual admin entry it may move to a Google Sheets/Excel-backed integration. Do not extend
or "productionize" the `X-Admin-Key` approach; a real redesign is expected before production use.

- **`AllowlistedEmail`** (`Id`, `Email`, `AddedAt`) — one row per email an admin has approved for
  beta signup. `Email` is always stored trimmed + lowercased before insert. Uniqueness is enforced
  by a unique index on `Email` *plus* strict normalization in code before every write/read
  comparison — the index alone is not case-insensitive, so a caller that skips normalization can
  still create a logical duplicate.
- **Admin auth is a completely separate concern from tester `ApiKey` identity** — it does not
  reuse or extend the `ApiKey`/`AuthenticationHandler` concept in any way, and the future tester
  `AuthenticationHandler` (Step 1.3) must not be merged with it. Admin endpoints require a header
  `X-Admin-Key` matching the `Admin:ApiKey` configuration value (set via `dotnet user-secrets`,
  the same mechanism already used for `Firebase:ServiceAccountPath`). Enforced by
  `RequireAdminKeyAttribute` (`SatelliteTracker.API.Filters`), a standalone action filter with no
  shared code with tester auth. Missing/invalid key → `401`.
- **Admin endpoints** (all under `api/admin`, all `[RequireAdminKey]`, all
  `[ApiExplorerSettings(IgnoreApi = true)]` so they never appear in the generated
  `openapi/sattrakk-api.json` Android consumes):
  - `POST /api/admin/allowlist` — adds an email (normalized) to the allowlist. Idempotent: adding
    an already-present email is a no-op success, not a conflict — this is an admin convenience
    tool, not a strict API contract.
  - `GET /api/admin/allowlist` — lists all allowlisted emails. No pagination (small beta group).
  - `POST /api/admin/reissue` — **stub, returns `501`**. Real key re-issuance (revoke the old key
    and issue a new one in one step, for a tester who lost their device) is not implemented yet.
    For now: an admin manually sets the old `ApiKey.IsActive = false` in the DB, and the tester
    re-runs `POST /api/auth/register`. Still gated by `X-Admin-Key` even though stubbed, so the
    real implementation doesn't need auth bolted on later.
- **`POST /api/auth/register`** (`AuthController`) — the self-service entry point, **not** behind
  `X-Admin-Key` or any tester auth, gated only by the allowlist check inside it. Flow: normalize
  the submitted email → reject with `403` if not on the allowlist → reject with `409` if an active
  `ApiKey` already exists for that email (message points at admin-assisted re-issuance, since
  `/api/admin/reissue` isn't functional yet) → otherwise generate a random 256-bit raw key, hash
  it with the existing `ApiKeyHasher`, persist a new `ApiKey` row, and return the **raw** key in
  the response body — the only time it is ever visible; it is never logged. Does **not** create a
  `UserSettings` row — that happens later, lazily, on the tester's first write to either
  `AlertMinutes` or `FcmToken` via `/api/settings/me` (Step 1.4; see below — it is **not**
  exclusively tied to FCM token registration, whichever of the two fields is written first is
  what triggers the row's creation).

### Tester authentication — the "ApiKey" scheme (Milestone E, Step 1.3)

Testers now authenticate as a real ASP.NET Core authentication scheme, not a one-off action
filter — this scheme is meant to be reused by every future tester-facing endpoint (starting with
`/api/settings/me` in Step 1.4), unlike the admin `X-Admin-Key` mechanism above which is
deliberately standalone and not meant to be extended.

- **`ApiKeyAuthenticationHandler`** (`SatelliteTracker.API.Authentication`, an
  `AuthenticationHandler<ApiKeyAuthenticationOptions>`) reads the `X-Api-Key` header, hashes it
  with the existing `ApiKeyHasher` (never reimplemented), and looks up the hash via the new
  `IApiKeyRepository.GetByHashAsync`. Registered as the **default** authentication scheme
  (`ApiKeyAuthenticationOptions.SchemeName = "ApiKey"`) in `Program.cs`, so `[Authorize]` works
  without per-action scheme configuration — endpoints still specify
  `[Authorize(AuthenticationSchemes = ApiKeyAuthenticationOptions.SchemeName)]` explicitly for
  clarity, matching the intent that more schemes could exist later.
- **Missing header → `NoResult()`, not `Fail()`** — this is what lets anonymous GET endpoints
  keep working normally even though the handler runs on every request; only an `[Authorize]`d
  action actually triggers a challenge for a missing/invalid key.
- **Uniform failure message, by design**: a missing header, a well-formed-but-unknown key, and a
  found-but-inactive key all produce the exact same `401` response (`HandleChallengeAsync` writes
  one fixed JSON body) — distinguishing them in the response would let a caller enumerate which
  keys exist or are active. `IApiKeyRepository.GetByHashAsync` deliberately does *not* filter on
  `IsActive` (unlike `GetActiveByEmailAsync`) so the handler can look up the row and check
  `IsActive` itself, collapsing both cases into the same generic failure.
- **`LastUsedAt` tracking**: every successful authentication calls the new
  `IApiKeyRepository.UpdateLastUsedAtAsync`, persisted immediately (not just set on the in-memory
  entity) so it reflects real API usage, not just registration/login events.
- On success, the handler builds a `ClaimsPrincipal` carrying the tester's `ApiKeyId`, `Email`,
  and `DisplayName`. Controllers read the ApiKeyId via `HttpContext.User.GetApiKeyId()` (or just
  `User.GetApiKeyId()` from within a controller) — an extension method in
  `SatelliteTracker.API.Authentication.ClaimsPrincipalExtensions` — rather than re-parsing the
  claim by hand in every action.
- **Endpoints requiring `[Authorize(AuthenticationSchemes = ApiKeyAuthenticationOptions.SchemeName)]`**:
  `POST`/`PUT /api/satellites`, `POST /api/tles/{satelliteId}/fetch`,
  `POST`/`PUT`/`DELETE` on notes, `PATCH /api/passes/{id}/notify`,
  `POST /api/calendar/schedule` (even though it's not itself a DB write — an anonymous
  calendar-generation endpoint is an abuse/DoS surface), `PUT /api/settings` (a mutating
  endpoint found during the Step 1.3 audit that wasn't in the original planned list), and all
  three `/api/settings/me*` actions from Step 1.4 below — **including its GET**, which is the
  first exception to "all GETs are anonymous" (see that section for why).
- Deliberately **not** touched: `POST /api/auth/register` (self-service, gated only by the
  allowlist check — see above) and every `api/admin/*` endpoint (gated by the separate
  `X-Admin-Key`/`RequireAdminKeyAttribute` mechanism, which must not be merged with tester auth).

### Per-tester settings lifecycle — /api/settings/me (Milestone E, Step 1.4)

`SettingsMeController` (`api/settings/me`) is where Android reads and writes a tester's own
`AlertMinutes`/`FcmToken`. All three actions — `GET`, `PUT`, and `PUT /fcm-token` — require
`[Authorize(AuthenticationSchemes = ApiKeyAuthenticationOptions.SchemeName)]`.

- **`GET /api/settings/me` is the first inherently-tester-specific GET in the API**, and thus the
  first exception to the "all GETs are anonymous" rule from Step 1.3 above — its response depends
  on which tester is asking, unlike every other GET endpoint, which is why it needs `[Authorize]`
  when no other GET does. Keep this in mind for future endpoints: a GET only needs auth when its
  *response*, not just mutating siblings on the same controller, is tester-specific.
- **The GET is strictly read-only.** If no `UserSettings` row exists yet for the caller's
  `ApiKeyId`, it returns a *computed* default (`AlertMinutes: []`, `FcmToken: null`) without ever
  inserting a row — `IUserSettingsRepository.GetByApiKeyIdAsync` returning a "not found" `Result`
  failure is treated as "use the default," the same pattern `SettingsController.GetOrDefault`
  already uses for the global `Settings` row.
- **Lazy creation, on first write to *either* field** — a `UserSettings` row is created on
  whichever of `PUT /api/settings/me` (`AlertMinutes`) or `PUT /api/settings/me/fcm-token`
  (`FcmToken`) is called first for that tester, not exclusively by FCM token registration. Both
  orderings are valid product flows (e.g. Android's settings screen may let a tester choose alert
  timing before the push-permission soft-ask/hard-ask flow ever completes, or vice versa).
- **Each PUT touches only its own field**, both on creation and on update — this is the reason
  `IUserSettingsRepository` gained two dedicated methods instead of reusing the existing
  `UpsertAsync(UserSettings)`, which overwrites both fields unconditionally and is unsafe for this
  use case:
  - `UpsertAlertMinutesAsync(apiKeyId, alertMinutes)` — on insert, sets `FcmToken = null`; on
    update, changes only `AlertMinutes` and `UpdatedAt`, leaving any existing `FcmToken` alone.
  - `UpsertFcmTokenAsync(apiKeyId, fcmToken)` — on insert, sets `AlertMinutes = []` (**not** the
    `UserSettings` entity's `[5, 10, 30]` default value, which only applies to
    directly-constructed entities elsewhere, e.g. tests); on update, changes only `FcmToken` and
    `UpdatedAt`, leaving any existing `AlertMinutes` alone.
  - Calling one endpoint must never null out or reset the field owned by the other — this was the
    critical regression risk this design guards against, and is covered explicitly by
    `SettingsMeEndpointTests`.
- **`AlertMinutes` valid value set is `{5, 10, 15, 30, 60}`**, validated in `SettingsMeController`
  (`ValidAlertMinutes`) — any value outside that set is rejected with `400` before the repository
  is touched. An empty array is valid and means "no alerts."
- `PUT /api/settings/me/fcm-token` rejects an empty or whitespace-only token with `400`.

### WebApplicationFactory test infrastructure

`SatelliteTracker.Tests.API.Infrastructure.CustomWebApplicationFactory` boots the real API host
(`Program.cs`, full middleware pipeline) in-memory via `Microsoft.AspNetCore.Mvc.Testing`, backed
by a SQLite in-memory `TestAppDbContext` (the same subclass/pattern `TestDbContextFactory` already
uses for repository-level tests — the int[]-as-CSV conversion for `UserSettings.AlertMinutes` and
the no-op `SeedData()` override). It also removes the three background `IHostedService`
registrations (`TleUpdateJob`, `PassCalculationJob`, `PassNotificationJob`) so tests don't race
real-world timers against N2YO/Firebase on an in-memory connection. Use this — not hand-built
`ActionExecutingContext`/filter-context objects like `RequireAdminKeyAttributeTests` uses — for any
test that needs to verify behavior of the actual HTTP pipeline (header parsing, DI wiring,
middleware ordering), since that's something a direct controller-method call can't exercise.
Controller-level unit tests (real SQLite-backed repositories, mocked services) remain the right
tool for testing business logic that doesn't depend on the pipeline itself.

---

## Code Conventions

### Patterns
- **Repository pattern** for all DB access — never call DbContext from controllers
- **Interface-first** — always define interface before implementation (IPassRepository, ITLEService)
- **Result<T> pattern** — services return Result<T> (or Result for no-value operations), never throw exceptions to controllers
- **Async/await everywhere** — no sync DB or HTTP calls

### Naming
- Entities: PascalCase singular (Satellite, TleRecord, Pass, Note, Settings, ApiKey, UserSettings)
- Repositories: I{Entity}Repository interface + {Entity}Repository implementation
- Services: I{Name}Service interface + {Name}Service implementation
- Tests: {Class}Tests.cs with xUnit

### Structure per Module
```
Modules/{ModuleName}/
    Entities/
    Repositories/
        I{Entity}Repository.cs
        {Entity}Repository.cs
    Services/
        I{Name}Service.cs
        {Name}Service.cs
    DTOs/
```

---

## Testing

- Framework: **xUnit**
- DB tests: **in-memory SQLite** (not mocks for repositories)
- Unit tests: standard mocks (Moq) for services
- File naming: `{Class}Tests.cs`
- Location: `SatelliteTracker.Tests/`

---

## API Conventions

- All endpoints prefixed with `/api`
- All times in UTC (display conversion on client)
- Response format: JSON
- Real-time endpoints (/position, /track) do NOT hit DB — go through Cache → N2YO
- All other endpoints work from DB only

---

## Git Strategy

| Branch      | Purpose                        |
|-------------|--------------------------------|
| main        | Stable — Production only       |
| develop     | Active development integration |
| feature/\*  | One feature per branch         |
| fix/\*      | Bug fixes                      |

CI runs on GitHub Actions: restore → build → test on every push/PR to main.

---

## Build Order (Milestones)

A → Infrastructure
B → TLEService + PassService + SGP4
C → React Web Frontend
D → Calendar Sync (ICS MVP) + FCM Push Notifications (current)
E → Android (Kotlin) basic
F → Sky View / ARCore
G → Testing + Production Deploy

---

## What NOT to do

- Never call N2YO API from controllers or frontend
- Never store real-time position data in DB
- Never add Redis — IMemoryCache is sufficient for current scale
- Never use Login/Auth — this is a personal/team app, no auth needed
- Never add map layers beyond standard (no topo, no satellite imagery)
- Never implement calendar event cancellation in the ICS model — out of scope by design, not a gap
