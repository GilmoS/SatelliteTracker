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

    /// <summary>
    /// Looks up an ApiKey by its hash, regardless of IsActive. Callers (the ApiKey
    /// AuthenticationHandler) are responsible for checking IsActive themselves — this
    /// distinction lets the handler apply a single uniform "invalid key" failure to both
    /// "not found" and "found but inactive" without leaking which one occurred.
    /// </summary>
    Task<Result<ApiKey>> GetByHashAsync(string keyHash);

    Task<Result<ApiKey>> CreateAsync(ApiKey apiKey);

    /// <summary>
    /// Persists LastUsedAt for a successfully authenticated key. Called on every authenticated
    /// request by the ApiKey AuthenticationHandler.
    /// </summary>
    Task<Result> UpdateLastUsedAtAsync(Guid apiKeyId, DateTimeOffset timestamp);
}
