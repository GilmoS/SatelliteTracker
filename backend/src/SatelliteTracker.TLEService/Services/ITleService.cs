using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.TLEService.Services;

// This interface defines the contract for the TLE service, which is responsible for fetching, saving, and managing Two-Line Element (TLE) data for satellites.
public interface ITleService
{
    Task<Result<TleRecord>> FetchAndSaveAsync(int noradId, CancellationToken ct = default);
    Task<Result<TleRecord>> GetLatestAsync(int noradId);
    Task<Result<bool>> IsTleStaleAsync(int noradId);
}
