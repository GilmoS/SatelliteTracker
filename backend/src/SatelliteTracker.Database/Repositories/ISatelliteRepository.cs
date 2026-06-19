using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

// This interface defines the contract for a repository that manages Satellite entities in the database.
// It provides methods for retrieving, adding, and updating satellite records,
// with each method returning a Result object that encapsulates the success or failure of the operation along with any relevant data or error messages.
public interface ISatelliteRepository
{
    Task<Result<IEnumerable<Satellite>>> GetAllAsync();
    Task<Result<Satellite>> GetByIdAsync(Guid id);
    Task<Result<Satellite>> GetByNoradIdAsync(int noradId);
    Task<Result<IEnumerable<Satellite>>> GetActiveAsync();
    Task<Result<Satellite>> AddAsync(Satellite satellite);
    Task<Result<Satellite>> UpdateAsync(Satellite satellite);
}
