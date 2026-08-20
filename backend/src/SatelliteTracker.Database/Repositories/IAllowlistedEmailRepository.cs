using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

// Manages the beta self-registration allowlist. Callers must pass already-normalized
// (trimmed, lowercased) emails — this repository does not normalize on their behalf.
public interface IAllowlistedEmailRepository
{
    Task<Result<bool>> ExistsAsync(string normalizedEmail);

    /// <summary>
    /// Idempotent insert: if the email is already allowlisted, returns the existing row as a
    /// success rather than an error.
    /// </summary>
    Task<Result<AllowlistedEmail>> AddAsync(string normalizedEmail);

    Task<Result<IEnumerable<AllowlistedEmail>>> GetAllAsync();
}
