using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

// This interface defines the contract for a repository that manages per-tester UserSettings entities.
public interface IUserSettingsRepository
{
    Task<Result<UserSettings>> GetByApiKeyIdAsync(Guid apiKeyId);
    Task<Result<IEnumerable<UserSettings>>> GetAllActiveAsync();
    Task<Result<UserSettings>> UpsertAsync(UserSettings settings);

    /// <summary>
    /// Creates a UserSettings row (FcmToken = null) or updates AlertMinutes on an existing one.
    /// Never touches FcmToken on an existing row — see CLAUDE.md's lazy-creation note.
    /// </summary>
    Task<Result<UserSettings>> UpsertAlertMinutesAsync(Guid apiKeyId, int[] alertMinutes);

    /// <summary>
    /// Creates a UserSettings row (AlertMinutes = []) or updates FcmToken on an existing one.
    /// Never touches AlertMinutes on an existing row — see CLAUDE.md's lazy-creation note.
    /// </summary>
    Task<Result<UserSettings>> UpsertFcmTokenAsync(Guid apiKeyId, string fcmToken);

    /// <summary>
    /// Clears a dead FcmToken (FCM reported it unregistered/invalid), but only if the row still
    /// holds <paramref name="expectedToken"/> — a fresh token the app synced in the meantime is
    /// never wiped. Returns <c>true</c> if the token was cleared.
    /// </summary>
    Task<Result<bool>> ClearFcmTokenAsync(Guid apiKeyId, string expectedToken);
}
