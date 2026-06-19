using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;
// This interface defines the contract for a repository that manages Settings entities in the database.
public interface ISettingsRepository
{
    Task<Result<Settings>> GetAsync();
    Task<Result<Settings>> UpsertAsync(Settings settings);
}
