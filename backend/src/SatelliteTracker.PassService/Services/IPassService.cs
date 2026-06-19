using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.PassService.SGP4;

namespace SatelliteTracker.PassService.Services;

// This interface defines the contract for a service that calculates and retrieves satellite passes.
public interface IPassService
{
    Task<Result<IEnumerable<PassResult>>> CalculateAndSavePassesAsync(Guid satelliteId, CancellationToken ct = default);
    Task<Result<IEnumerable<Pass>>> GetUpcomingPassesAsync(Guid satelliteId);
    Task<Result<IEnumerable<Pass>>> GetPassHistoryAsync(Guid satelliteId);
    Task<Result<Pass>> GetPassByIdAsync(Guid passId);
}
