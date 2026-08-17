using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

// This interface defines the contract for a repository that manages per-tester UserSettings entities.
public interface IUserSettingsRepository
{
    Task<Result<UserSettings>> GetByApiKeyIdAsync(Guid apiKeyId);
    Task<Result<IEnumerable<UserSettings>>> GetAllActiveAsync();
    Task<Result<UserSettings>> UpsertAsync(UserSettings settings);
}
