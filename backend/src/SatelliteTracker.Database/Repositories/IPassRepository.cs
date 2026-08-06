using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;
// This interface defines the contract for a repository that manages Pass entities in the database.
// It includes methods for retrieving upcoming and historical passes, getting a pass by its ID, adding new passes, and updating existing passes.
// Each method returns a Result object that indicates success or failure and contains the relevant data or error information.
public interface IPassRepository
{
    Task<Result<IEnumerable<Pass>>> GetUpcomingAsync(Guid satelliteId, DateTime from, DateTime to);
    Task<Result<IEnumerable<Pass>>> GetHistoryAsync(Guid satelliteId, DateTime from);
    Task<Result<Pass>> GetByIdAsync(Guid id);
    Task<Result<Pass>> AddAsync(Pass pass);
    Task<Result<bool>> AddRangeAsync(IEnumerable<Pass> passes);
    Task<Result<Pass>> UpdateAsync(Pass pass);
    Task<Result<bool>> DeleteUpcomingAsync(Guid satelliteId, DateTime from);
    Task<Result<Pass>> UpdateNotifyAsync(Guid id, bool notify);
    Task<Result<IEnumerable<Pass>>> GetPendingNotificationsAsync();

    /// <summary>
    /// Retrieves passes by ID, with <c>Satellite</c> eager-loaded. Used by the calendar schedule
    /// flow to map straight to <see cref="OutlookService.Abstractions.CalendarEventData"/>
    /// without a second round-trip.
    /// </summary>
    /// <returns>
    /// A failure listing every ID that doesn't exist if any are missing — never a partial list.
    /// </returns>
    Task<Result<IReadOnlyList<Pass>>> GetByIdsAsync(IEnumerable<Guid> passIds);

    /// <summary>
    /// Bulk-sets <see cref="Pass.OutlookSynced"/> to <c>true</c> for the given passes.
    /// The field name is retained for forward-compatibility with a possible future Microsoft
    /// Graph implementation. In the current ICS MVP it means "an .ics was generated and offered
    /// to the user" — not "confirmed added to a calendar" — since the ICS model has no
    /// programmatic way to confirm the user actually imported the event.
    /// </summary>
    Task<Result> MarkOutlookSyncedAsync(IEnumerable<Guid> passIds);
}
