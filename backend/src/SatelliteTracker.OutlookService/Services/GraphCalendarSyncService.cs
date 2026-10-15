using SatelliteTracker.Database.Common;
using SatelliteTracker.OutlookService.Abstractions;

namespace SatelliteTracker.OutlookService.Services;

/// <summary>
/// Placeholder Microsoft Graph API implementation of <see cref="ICalendarSyncService"/>. Pending
/// IT admin consent for org calendar access — this is genuinely future work, not a permanent stub.
/// </summary>
public class GraphCalendarSyncService : ICalendarSyncService
{
    /// <inheritdoc />
    public Task<Result<CalendarSyncOutcome>> SyncPassesAsync(
        IReadOnlyList<CalendarEventData> passes,
        CalendarSyncSettings settings,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "Microsoft Graph calendar sync is pending IT admin consent for org calendar access.");
    }

    /// <inheritdoc />
    public Task<Result> CancelSyncedPassesAsync(
        IReadOnlyList<CalendarEventData> passes,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "Microsoft Graph calendar sync is pending IT admin consent for org calendar access.");
    }
}
