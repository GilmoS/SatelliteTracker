using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;
// This interface defines the contract for a repository that manages TleRecord entities in the database.
public interface ITleRepository
{
    Task<Result<TleRecord>> GetLatestByNoradIdAsync(int noradId);
    Task<Result<TleRecord>> GetByIdAsync(Guid id);
    Task<Result<IEnumerable<TleRecord>>> GetHistoryAsync(int noradId, DateTime from);
    Task<Result<TleRecord>> AddAsync(TleRecord tle);
    Task<Result<bool>> DeleteOlderThanAsync(DateTime cutoff);
}
