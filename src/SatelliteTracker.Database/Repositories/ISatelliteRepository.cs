using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

public interface ISatelliteRepository
{
    Task<Result<IEnumerable<Satellite>>> GetAllAsync();
    Task<Result<Satellite>> GetByIdAsync(Guid id);
    Task<Result<Satellite>> GetByNoradIdAsync(int noradId);
    Task<Result<IEnumerable<Satellite>>> GetActiveAsync();
    Task<Result<Satellite>> AddAsync(Satellite satellite);
    Task<Result<Satellite>> UpdateAsync(Satellite satellite);
}
