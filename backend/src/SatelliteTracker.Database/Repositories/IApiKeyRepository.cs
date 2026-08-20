using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

public interface IApiKeyRepository
{
    /// <summary>
    /// Looks up an active (IsActive = true) ApiKey by email. Callers must pass an already
    /// normalized (trimmed, lowercased) email. Failure means "none found", not necessarily
    /// an error — a revoked or never-registered email is expected to fail here.
    /// </summary>
    Task<Result<ApiKey>> GetActiveByEmailAsync(string normalizedEmail);

    Task<Result<ApiKey>> CreateAsync(ApiKey apiKey);
}
