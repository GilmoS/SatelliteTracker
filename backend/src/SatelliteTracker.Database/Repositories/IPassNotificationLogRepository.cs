using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

// Notification attempt records (PassNotificationLog) — one row per (pass, tester, threshold),
// claimed before the FCM send and updated with the real outcome afterwards.
public interface IPassNotificationLogRepository
{
    /// <summary>Returns every logged send for the given passes, across all testers/thresholds.</summary>
    Task<Result<IEnumerable<PassNotificationLog>>> GetByPassIdsAsync(IEnumerable<Guid> passIds);

    /// <summary>
    /// Inserts one attempt record — the claim made before the first send. If a row already exists
    /// for the (PassId, ApiKeyId, AlertMinutes) key — including a concurrent insert from another
    /// job tick — this does not throw; it returns <c>false</c> to mean "already claimed, don't send".
    /// </summary>
    Task<Result<bool>> TryInsertAsync(PassNotificationLog log);

    /// <summary>
    /// Atomically claims a Failed row for a retry: moves it to Pending and increments AttemptCount,
    /// but only if it is still Failed with <paramref name="expectedAttemptCount"/> attempts.
    /// Returns <c>false</c> when another tick got there first.
    /// </summary>
    Task<Result<bool>> TryClaimRetryAsync(Guid logId, int expectedAttemptCount, DateTimeOffset attemptAt);

    /// <summary>
    /// Records the outcome of the attempt in flight. <see cref="PassNotificationStatus.Sent"/> sets
    /// SentAt and clears LastError; any other status stores <paramref name="error"/>.
    /// </summary>
    Task<Result> RecordOutcomeAsync(Guid logId, PassNotificationStatus status, string? error, DateTimeOffset at);

    /// <summary>
    /// Deletes all log rows for a pass, independent of whether the Pass row itself is deleted.
    /// Not currently called by any job — see CLAUDE.md for the retention/cleanup note.
    /// </summary>
    Task<Result> DeleteByPassIdAsync(Guid passId);
}
