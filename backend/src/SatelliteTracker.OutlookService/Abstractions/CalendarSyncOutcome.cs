namespace SatelliteTracker.OutlookService.Abstractions;

/// <summary>
/// Result of a successful <see cref="ICalendarSyncService.SyncPassesAsync"/> call.
/// </summary>
/// <param name="IcsContent">Full combined .ics file text, containing one VEVENT per synced pass.</param>
/// <param name="Uids">UIDs of the events included in <see cref="IcsContent"/>, in the same order as the input passes.</param>
public record CalendarSyncOutcome(string IcsContent,IReadOnlyList<string> Uids);
