using SatelliteTracker.Database.Entities;
using SatelliteTracker.OutlookService.Abstractions;

namespace SatelliteTracker.OutlookService.Mapping;

/// <summary>
/// Converts <see cref="Pass"/> entities into calendar-agnostic <see cref="CalendarEventData"/> DTOs.
/// </summary>
public static class CalendarEventMapper
{
    /// <summary>
    /// Maps a <see cref="Pass"/> to <see cref="CalendarEventData"/>. Requires <c>pass.Satellite</c>
    /// to be loaded (e.g. via <c>.Include(p => p.Satellite)</c> upstream).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when <c>pass.Satellite</c> is null.</exception>
    public static CalendarEventData ToCalendarEventData(this Pass pass)
    {
        if (pass.Satellite is null)
        {
            throw new InvalidOperationException(
                $"Pass {pass.Id} has no loaded Satellite. Ensure the query includes .Include(p => p.Satellite).");
        }

        // {norad_id}-{orbit_number}: stable, monotonic, and unique per physical pass, so a
        // re-sync updates the existing calendar event instead of creating a duplicate. Accepted
        // limitation: if a TLE re-snapshot shifts orbit_number by one at an orbit-count boundary
        // for the same physical pass, re-sync can produce a duplicate event instead of an update.
        // This is rare, low-severity (user deletes the stray duplicate manually), and deliberately
        // not engineered around — see CLAUDE.md's Calendar Sync section.
        var uid = $"{pass.Satellite.NoradId}-{pass.OrbitNumber}@sattrakk.com";

        return new CalendarEventData(
            Uid: uid,
            SatelliteName: pass.Satellite.Name,
            NoradId: pass.Satellite.NoradId,
            OrbitNumber: pass.OrbitNumber,
            Aos: new DateTimeOffset(DateTime.SpecifyKind(pass.Aos, DateTimeKind.Utc)),
            Los: new DateTimeOffset(DateTime.SpecifyKind(pass.Los, DateTimeKind.Utc)),
            MaxElevation: pass.MaxElevation,
            AosAzimuth: pass.AosAzimuth,
            LosAzimuth: pass.LosAzimuth);
    }
}
