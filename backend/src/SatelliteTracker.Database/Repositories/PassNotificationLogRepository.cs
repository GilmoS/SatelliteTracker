using Microsoft.EntityFrameworkCore;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

public class PassNotificationLogRepository : IPassNotificationLogRepository
{
    private readonly AppDbContext _context;

    public PassNotificationLogRepository(AppDbContext context) => _context = context;

    public async Task<Result<IEnumerable<PassNotificationLog>>> GetByPassIdsAsync(IEnumerable<Guid> passIds)
    {
        try
        {
            var ids = passIds.ToList();
            var logs = await _context.PassNotificationLogs
                .Where(l => ids.Contains(l.PassId))
                .ToListAsync();

            return Result<IEnumerable<PassNotificationLog>>.Success(logs);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<PassNotificationLog>>.Failure(ex.Message);
        }
    }

    public async Task<Result<bool>> TryInsertAsync(PassNotificationLog log)
    {
        try
        {
            _context.PassNotificationLogs.Add(log);
            await _context.SaveChangesAsync();
            return Result<bool>.Success(true);
        }
        catch (DbUpdateException)
        {
            // Unique (PassId, ApiKeyId, AlertMinutes) violation — another tick already claimed
            // this threshold. Detach so the context stays usable, and treat this as "already claimed".
            _context.Entry(log).State = EntityState.Detached;
            return Result<bool>.Success(false);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(ex.Message);
        }
    }

    public async Task<Result<bool>> TryClaimRetryAsync(Guid logId, int expectedAttemptCount, DateTimeOffset attemptAt)
    {
        try
        {
            var claimed = await _context.PassNotificationLogs
                .Where(l => l.Id == logId
                            && l.Status == PassNotificationStatus.Failed
                            && l.AttemptCount == expectedAttemptCount)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(l => l.Status, PassNotificationStatus.Pending)
                    .SetProperty(l => l.AttemptCount, expectedAttemptCount + 1)
                    .SetProperty(l => l.LastAttemptAt, attemptAt));

            return Result<bool>.Success(claimed == 1);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(ex.Message);
        }
    }

    public async Task<Result> RecordOutcomeAsync(Guid logId, PassNotificationStatus status, string? error, DateTimeOffset at)
    {
        try
        {
            var sentAt = status == PassNotificationStatus.Sent ? at : (DateTimeOffset?)null;
            var lastError = status == PassNotificationStatus.Sent ? null : Truncate(error, MaxErrorLength);

            await _context.PassNotificationLogs
                .Where(l => l.Id == logId)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(l => l.Status, status)
                    .SetProperty(l => l.SentAt, sentAt)
                    .SetProperty(l => l.LastError, lastError));

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    private const int MaxErrorLength = 1000; // Matches the LastError column length in AppDbContext

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];

    public async Task<Result> DeleteByPassIdAsync(Guid passId)
    {
        try
        {
            var rows = await _context.PassNotificationLogs
                .Where(l => l.PassId == passId)
                .ToListAsync();

            if (rows.Count > 0)
            {
                _context.PassNotificationLogs.RemoveRange(rows);
                await _context.SaveChangesAsync();
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}
