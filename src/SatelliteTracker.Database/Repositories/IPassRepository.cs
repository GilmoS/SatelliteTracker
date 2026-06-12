using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

public interface IPassRepository
{
    Task<Result<IEnumerable<Pass>>> GetUpcomingAsync(Guid satelliteId, DateTime from, DateTime to);
    Task<Result<IEnumerable<Pass>>> GetHistoryAsync(Guid satelliteId, DateTime from);
    Task<Result<Pass>> GetByIdAsync(Guid id);
    Task<Result<Pass>> AddAsync(Pass pass);
    Task<Result<bool>> AddRangeAsync(IEnumerable<Pass> passes);
    Task<Result<Pass>> UpdateAsync(Pass pass);
}
