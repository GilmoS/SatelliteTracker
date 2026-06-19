using SatelliteTracker.Database.Common;
using SatelliteTracker.TLEService.Client.DTOs;

namespace SatelliteTracker.TLEService.Client;

// This interface defines the contract for a client that interacts with the N2YO API to retrieve satellite data, including TLE, position, and radio pass information
public interface IN2YOClient
{
    Task<Result<TleResponse>> GetTleAsync(int noradId, CancellationToken ct = default);

    Task<Result<PositionResponse>> GetPositionsAsync(int noradId, double lat, double lng, double alt, int seconds,CancellationToken ct = default);

    Task<Result<RadioPassResponse>> GetRadioPassesAsync(int noradId, double lat, double lng, double alt, int days, int minElevation, CancellationToken ct = default);
}
