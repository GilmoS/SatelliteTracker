using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

public interface INoteRepository
{
    Task<Result<IEnumerable<Note>>> GetByPassIdAsync(Guid passId);
    Task<Result<Note>> GetByIdAsync(Guid id);
    Task<Result<Note>> AddAsync(Note note);
    Task<Result<Note>> UpdateAsync(Note note);
    Task<Result<bool>> DeleteAsync(Guid id);
}
