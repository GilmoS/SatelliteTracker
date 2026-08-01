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

        // TODO: placeholder UID based on pass.Id — replace once the orbit_number source
        // (TLE Revolution Number at Epoch vs. computed) is confirmed and a final UID formula is decided.
        var uid = $"pass-placeholder-{pass.Id}";

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
