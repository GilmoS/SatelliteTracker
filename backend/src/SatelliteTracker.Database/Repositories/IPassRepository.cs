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
}
