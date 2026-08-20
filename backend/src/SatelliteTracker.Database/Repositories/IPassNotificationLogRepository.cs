using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

// Append-only ledger of notifications actually sent (PassNotificationLog) — one row per
// (pass, tester, threshold) that fired, never a flag that gets flipped back.
public interface IPassNotificationLogRepository
{
    /// <summary>Returns every logged send for the given passes, across all testers/thresholds.</summary>
    Task<Result<IEnumerable<PassNotificationLog>>> GetByPassIdsAsync(IEnumerable<Guid> passIds);

    /// <summary>
    /// Inserts one send record. If a row already exists for the (PassId, ApiKeyId, AlertMinutes)
    /// key — including a concurrent insert from another job tick — this does not throw; it
    /// returns <c>false</c> to mean "already logged, treat as already sent".
    /// </summary>
    Task<Result<bool>> TryInsertAsync(PassNotificationLog log);

    /// <summary>
    /// Deletes all log rows for a pass, independent of whether the Pass row itself is deleted.
    /// Not currently called by any job — see CLAUDE.md for the retention/cleanup note.
    /// </summary>
    Task<Result> DeleteByPassIdAsync(Guid passId);
}
