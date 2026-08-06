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

Five tables. All PKs are UUID (Guid in C#). All relationships via Fluent API in OnModelCreating.

| Table      | Purpose                                              |
|------------|------------------------------------------------------|
| satellites | Satellite catalog. NoradId is unique.                |
| tles       | TLE history per satellite. Up to 6 months back.      |
| passes     | Calculated passes. 1 week forward + 6 months history.|
| notes      | Free-text per pass. CASCADE delete with pass.        |
| settings   | Single settings row. AlertMinutes is int[].          |

---

## Code Conventions

### Patterns
- **Repository pattern** for all DB access — never call DbContext from controllers
- **Interface-first** — always define interface before implementation (IPassRepository, ITLEService)
- **Result<T> pattern** — services return Result<T> (or Result for no-value operations), never throw exceptions to controllers
- **Async/await everywhere** — no sync DB or HTTP calls

### Naming
- Entities: PascalCase singular (Satellite, TleRecord, Pass, Note, Settings)
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
