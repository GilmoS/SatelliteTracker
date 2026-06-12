using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.TLEService.Services;

public interface ITleService
{
    Task<Result<TleRecord>> FetchAndSaveAsync(int noradId, CancellationToken ct = default);
    Task<Result<TleRecord>> GetLatestAsync(int noradId);
    Task<Result<bool>> IsTleStaleAsync(int noradId);
}
