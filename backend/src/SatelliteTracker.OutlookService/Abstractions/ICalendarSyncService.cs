using SatelliteTracker.Database.Common;

namespace SatelliteTracker.OutlookService.Abstractions;

/// <summary>
/// Abstraction boundary for syncing satellite passes to a user's calendar. Swappable between
/// the current ICS-file MVP and a future Microsoft Graph implementation via a single DI change,
/// with no changes required in API/PassService/Controllers.
/// </summary>
public interface ICalendarSyncService
{
    /// <summary>
    /// Produces calendar events for the given passes according to the supplied settings.
    /// </summary>
    Task<Result<CalendarSyncOutcome>> SyncPassesAsync( IReadOnlyList<CalendarEventData> passes, CalendarSyncSettings settings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels/removes previously synced calendar events for the given passes.
    /// </summary>
    Task<Result> CancelSyncedPassesAsync( IReadOnlyList<CalendarEventData> passes,CancellationToken cancellationToken = default);
}
