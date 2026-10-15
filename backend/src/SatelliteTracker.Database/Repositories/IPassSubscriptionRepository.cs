using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

// Manages per-tester notification opt-ins for specific passes (PassSubscription). The table is
// sparse — a row exists only once a tester has actively toggled notifications on for a pass —
// so every read here must apply the opt-in default (no row = not notified) explicitly.
public interface IPassSubscriptionRepository
{
    /// <summary>
    /// Whether <paramref name="apiKeyId"/> should be notified about <paramref name="passId"/>.
    /// LEFT JOIN + COALESCE semantics: false unless an explicit opt-in row exists.
    /// </summary>
    Task<Result<bool>> GetEffectiveNotifyStatusAsync(Guid passId, Guid apiKeyId);

    /// <summary>
    /// Returns every override row (any Notify value) for the given passes. Callers must still
    /// apply the sparse default (missing pair = not notified) for pairs not present in the result.
    /// </summary>
    Task<Result<IEnumerable<PassSubscription>>> GetByPassIdsAsync(IEnumerable<Guid> passIds);

    Task<Result<PassSubscription>> SetNotifyAsync(Guid passId, Guid apiKeyId, bool notify);

    /// <summary>
    /// Deletes the single (passId, apiKeyId) override row, if one exists — a no-op success if
    /// not. Used by PATCH /api/passes/{id}/notify when a tester sets Notify back to false: since
    /// false is the sparse default, the row is removed rather than overwritten with a redundant
    /// "false" row, keeping the table strictly sparse.
    /// </summary>
    Task<Result> DeleteOverrideAsync(Guid passId, Guid apiKeyId);

    /// <summary>
    /// Deletes all opt-out rows for a pass, independent of whether the Pass row itself is
    /// deleted. Not currently called by any job — see CLAUDE.md for the retention/cleanup note.
    /// </summary>
    Task<Result> DeleteByPassIdAsync(Guid passId);
}
