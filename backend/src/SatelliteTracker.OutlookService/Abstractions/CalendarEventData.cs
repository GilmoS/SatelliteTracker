namespace SatelliteTracker.OutlookService.Abstractions;

/// <summary>
/// Calendar-agnostic representation of a single satellite pass, used as the input to
/// any <see cref="ICalendarSyncService"/> implementation. Keeps calendar sync decoupled
/// from the EF Core <c>Pass</c>/<c>Satellite</c> entities.
/// </summary>
/// <param name="Uid">Stable identifier for the calendar event tied to this pass.</param>
/// <param name="SatelliteName">Display name of the satellite being tracked.</param>
/// <param name="NoradId">NORAD catalog ID of the satellite.</param>
/// <param name="OrbitNumber">Orbit number of this pass.</param>
/// <param name="Aos">Acquisition of signal (pass start) time.</param>
/// <param name="Los">Loss of signal (pass end) time.</param>
/// <param name="MaxElevation">Maximum elevation reached during the pass, in degrees.</param>
/// <param name="AosAzimuth">Azimuth at acquisition of signal, in degrees.</param>
/// <param name="LosAzimuth">Azimuth at loss of signal, in degrees.</param>
public record CalendarEventData(
    string Uid,
    string SatelliteName,
    int NoradId,
    int OrbitNumber,
    DateTimeOffset Aos,
    DateTimeOffset Los,
    decimal MaxElevation,
    decimal AosAzimuth,
    decimal LosAzimuth);
