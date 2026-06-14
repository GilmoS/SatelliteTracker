using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;
// This interface defines the contract for a repository that manages Note entities in the database.
// It provides methods for retrieving, adding, updating, and deleting notes based on their unique identifiers and associated pass IDs.
// Each method returns a Result object that encapsulates the success status and any relevant data or error messages.
public interface INoteRepository
{
    Task<Result<IEnumerable<Note>>> GetByPassIdAsync(Guid passId);
    Task<Result<Note>> GetByIdAsync(Guid id);
    Task<Result<Note>> AddAsync(Note note);
    Task<Result<Note>> UpdateAsync(Note note);
    Task<Result<bool>> DeleteAsync(Guid id);
}
