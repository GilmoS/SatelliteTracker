cat << 'EOF'
# CLAUDE.md — Satellite Pass Tracker

## Project Context
Satellite pass tracking system for Israel Aerospace Industries (IAI).
Tracks satellite passes over Israel, displays 2D/3D maps, integrates with Microsoft Outlook 365, and includes an Android app with AR Sky View mode.
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
dotnet run --project src/SatelliteTracker.API

# Run all tests
dotnet test

# Run a single test project
dotnet test src/<TestProject>/<TestProject>.csproj

# Run a single test by name
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Add a new EF Core migration
dotnet ef migrations add <MigrationName> --project src/SatelliteTracker.Database

# Apply migrations
dotnet ef database update --project src/SatelliteTracker.Database
```

Connection string: `ConnectionStrings:DefaultConnection` in `appsettings.Development.json` (not committed).

---

## Architecture — Modular Monolith

One .NET solution, five logical modules. Clients (Web + Android) talk ONLY to our backend.
The backend is the ONLY entity that communicates with N2YO and Microsoft Graph API.

```
SatelliteTracker.API            → Controllers, Routing, Validation (thin layer)
SatelliteTracker.TLEService     → N2YO client, TLE parsing, scheduled fetch job
SatelliteTracker.PassService    → SGP4 pass calculation logic
SatelliteTracker.OutlookService → Microsoft Graph API, calendar event creation
SatelliteTracker.Database       → EF Core, Migrations, Repositories
```

### Single Source of Truth Rules
- Web and Android clients NEVER call N2YO or Microsoft Graph directly
- The N2YO API key lives ONLY on the backend — never exposed to clients
- All data flows through one point: our API

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
- **Result<T> pattern** — services return Result<T>, never throw exceptions to controllers
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

A → Infrastructure (current)
B → TLEService + PassService + SGP4
C → React Web Frontend
D → Outlook + FCM Push Notifications
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
EOF
Output

# CLAUDE.md — Satellite Pass Tracker

## Project Context
Satellite pass tracking system for Israel Aerospace Industries (IAI).
Tracks satellite passes over Israel, displays 2D/3D maps, integrates with Microsoft Outlook 365, and includes an Android app with AR Sky View mode.
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
dotnet run --project src/SatelliteTracker.API

# Run all tests
dotnet test

# Run a single test project
dotnet test src/<TestProject>/<TestProject>.csproj

# Run a single test by name
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Add a new EF Core migration
dotnet ef migrations add <MigrationName> --project src/SatelliteTracker.Database

# Apply migrations
dotnet ef database update --project src/SatelliteTracker.Database
```

Connection string: `ConnectionStrings:DefaultConnection` in `appsettings.Development.json` (not committed).

---

## Architecture — Modular Monolith

One .NET solution, five logical modules. Clients (Web + Android) talk ONLY to our backend.
The backend is the ONLY entity that communicates with N2YO and Microsoft Graph API.

```
SatelliteTracker.API            → Controllers, Routing, Validation (thin layer)
SatelliteTracker.TLEService     → N2YO client, TLE parsing, scheduled fetch job
SatelliteTracker.PassService    → SGP4 pass calculation logic
SatelliteTracker.OutlookService → Microsoft Graph API, calendar event creation
SatelliteTracker.Database       → EF Core, Migrations, Repositories
```

### Single Source of Truth Rules
- Web and Android clients NEVER call N2YO or Microsoft Graph directly
- The N2YO API key lives ONLY on the backend — never exposed to clients
- All data flows through one point: our API

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
- **Result<T> pattern** — services return Result<T>, never throw exceptions to controllers
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
C → React Web Frontend (current)
D → Outlook + FCM Push Notifications
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
