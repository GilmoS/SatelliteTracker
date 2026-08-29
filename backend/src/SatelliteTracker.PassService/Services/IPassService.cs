using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.PassService.SGP4;

namespace SatelliteTracker.PassService.Services;

// This interface defines the contract for a service that calculates and retrieves satellite passes.
public interface IPassService
{
    Task<Result<IEnumerable<PassResult>>> CalculateAndSavePassesAsync(Guid satelliteId, CancellationToken ct = default);
    Task<Result<IEnumerable<Pass>>> GetUpcomingPassesAsync(Guid satelliteId);

    /// <summary>
    /// Retrieves the satellite's pass history (up to 6 months back), applying <paramref name="query"/>'s
    /// optional per-field filters and pagination. See <see cref="IPassRepository.GetHistoryAsync"/>.
    /// </summary>
    Task<Result<PagedResult<Pass>>> GetPassHistoryAsync(Guid satelliteId, PassHistoryQuery query);

    Task<Result<Pass>> GetPassByIdAsync(Guid passId);

    /// <summary>
    /// Computes the ground track for a specific, already-calculated pass across its stored
    /// [Aos, Los] window, using the TLE that was in effect when the pass was calculated
    /// (<see cref="Pass.TleId"/>) — not the satellite's currently-latest TLE.
    /// </summary>
    Task<Result<IEnumerable<GroundTrackPoint>>> GetPassTrackAsync(Guid passId);
}
