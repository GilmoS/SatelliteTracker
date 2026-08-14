# openapi/sattrakk-api.json

Static OpenAPI 3.0 spec for the SatTrakk backend, exported from `SatelliteTracker.API`
via the Swashbuckle CLI. The Android app's OpenAPI Generator Gradle plugin
(`android/app/build.gradle.kts`) reads this file at build time to generate its DTO layer —
it does not hit a running backend or Swagger UI.

## Regenerating after a controller/DTO change

```bash
cd backend/src
dotnet build SatelliteTracker.sln -c Debug
cd SatelliteTracker.API
dotnet tool run swagger tofile --output ../../../openapi/sattrakk-api.json bin/Debug/net9.0/SatelliteTracker.API.dll v1
```

The `swagger` CLI is a local dotnet tool pinned in `backend/.config/dotnet-tools.json`
(matched to the `Swashbuckle.AspNetCore` package version used by the API project) —
run `dotnet tool restore` from `backend/` first if it isn't installed yet.

Commit the regenerated file alongside the backend change that caused it, so the Android
build always has a spec that matches the API it will actually talk to.
