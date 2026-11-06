# syntax=docker/dockerfile:1
#
# One Dockerfile, two images (build context: backend/):
#   docker build --platform linux/amd64 --target api      -t sattrakk-api:local      backend/
#   docker build --platform linux/amd64 --target migrator -t sattrakk-migrator:local backend/
#
# No secrets enter any stage. All configuration (connection string, API keys, Firebase key) is
# supplied when a container runs; the API refuses to start without it (see CLAUDE.md, I2.2).
# There is deliberately no HEALTHCHECK: Caddy actively checks /health/live instead.

# ---------------------------------------------------------------------------------------------
# build: restores and copies the source once for both images.
# ---------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Layer order is for the build cache: the inputs that change least come first. The tool manifest
# and the .sln/.csproj files change rarely, so the tool restore and the package restore (the slow,
# network-bound steps) are reused from cache until a dependency changes. Ordinary source edits
# only invalidate the layers from "COPY . ." onwards.
COPY .config/dotnet-tools.json .config/
RUN dotnet tool restore

COPY SatelliteTracker.sln ./
COPY src/SatelliteTracker.API/SatelliteTracker.API.csproj                         src/SatelliteTracker.API/
COPY src/SatelliteTracker.Database/SatelliteTracker.Database.csproj               src/SatelliteTracker.Database/
COPY src/SatelliteTracker.OutlookService/SatelliteTracker.OutlookService.csproj   src/SatelliteTracker.OutlookService/
COPY src/SatelliteTracker.PassService/SatelliteTracker.PassService.csproj         src/SatelliteTracker.PassService/
COPY src/SatelliteTracker.TLEService/SatelliteTracker.TLEService.csproj           src/SatelliteTracker.TLEService/
COPY src/SatelliteTracker.Tests/SatelliteTracker.Tests.csproj                     src/SatelliteTracker.Tests/
# The solution restore covers the API publish. The second restore adds the linux-x64 runtime
# packs the self-contained migration bundle needs, so they are cached here too. Order matters:
# a later solution restore would rewrite the Database project's assets without linux-x64.
RUN dotnet restore SatelliteTracker.sln \
 && dotnet restore src/SatelliteTracker.Database/SatelliteTracker.Database.csproj -r linux-x64

COPY . .

# The two outputs come from separate stages on top of "build", so BuildKit skips the one a
# --target doesn't need and runs both in parallel when both images are built. (The bundle is a
# linux-x64 self-contained build, so it never shared compiled output with the API publish.)

# API: framework-dependent (the runtime ships in the aspnet base image). No apphost, since the
# entrypoint is "dotnet SatelliteTracker.API.dll".
FROM build AS publish-api
RUN dotnet publish src/SatelliteTracker.API/SatelliteTracker.API.csproj \
      -c Release --no-restore -p:UseAppHost=false -o /out/api

# Migration bundle: a self-contained linux-x64 executable that applies pending migrations.
# The Database project is its own startup project and provides DesignTimeDbContextFactory, so
# Program.cs (and its configuration validation) never runs here. Creating the bundle does not
# connect to a database; the connection string is passed when the bundle runs (--connection).
FROM build AS bundle
RUN dotnet ef migrations bundle \
      --project src/SatelliteTracker.Database \
      --startup-project src/SatelliteTracker.Database \
      --configuration Release \
      --self-contained -r linux-x64 \
      --output /out/migrator/efbundle

# ---------------------------------------------------------------------------------------------
# migrator: runs once per deploy, applies migrations, exits. Contains only the bundle.
# ---------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0 AS migrator
WORKDIR /app
COPY --from=bundle /out/migrator/efbundle ./efbundle
# Built-in non-root user (APP_UID is defined by the .NET base images).
USER $APP_UID
ENTRYPOINT ["./efbundle"]

# ---------------------------------------------------------------------------------------------
# api: the long-running API. Last stage, so a build without --target produces this image.
# ---------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS api
WORKDIR /app
# ASPNETCORE_ENVIRONMENT is deliberately unset: it defaults to Production.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# Files stay owned by root, so the app user can read but not modify them.
COPY --from=publish-api /out/api ./
USER $APP_UID
ENTRYPOINT ["dotnet", "SatelliteTracker.API.dll"]
