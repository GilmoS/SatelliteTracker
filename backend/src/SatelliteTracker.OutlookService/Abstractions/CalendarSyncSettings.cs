namespace SatelliteTracker.OutlookService.Abstractions;

/// <summary>
/// User-configurable options that shape how calendar events are generated.
/// </summary>
/// <param name="AlertMinutes">Reminder offsets (in minutes before AOS) to attach to each event.</param>
/// <param name="TeamEmail">Optional team email to associate with synced events; unused by the ICS MVP.</param>
public record CalendarSyncSettings(
    IReadOnlyList<int> AlertMinutes,
    string? TeamEmail);
