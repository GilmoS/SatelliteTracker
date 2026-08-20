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
            // Unique (PassId, ApiKeyId, AlertMinutes) violation — another tick already logged
            // this send. Detach so the context stays usable, and treat this as "already sent".
            _context.Entry(log).State = EntityState.Detached;
            return Result<bool>.Success(false);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(ex.Message);
        }
    }

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
