using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

public interface ITleRepository
{
    Task<Result<TleRecord>> GetLatestByNoradIdAsync(int noradId);
    Task<Result<IEnumerable<TleRecord>>> GetHistoryAsync(int noradId, DateTime from);
    Task<Result<TleRecord>> AddAsync(TleRecord tle);
    Task<Result<bool>> DeleteOlderThanAsync(DateTime cutoff);
}
