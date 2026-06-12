using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

public interface ISettingsRepository
{
    Task<Result<Settings>> GetAsync();
    Task<Result<Settings>> UpsertAsync(Settings settings);
}
