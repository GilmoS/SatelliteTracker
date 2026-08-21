using Microsoft.EntityFrameworkCore;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

public class PassSubscriptionRepository : IPassSubscriptionRepository
{
    private readonly AppDbContext _context;

    public PassSubscriptionRepository(AppDbContext context) => _context = context;

    public async Task<Result<bool>> GetEffectiveNotifyStatusAsync(Guid passId, Guid apiKeyId)
    {
        try
        {
            var subscription = await _context.PassSubscriptions
                .FirstOrDefaultAsync(s => s.PassId == passId && s.ApiKeyId == apiKeyId);

            // No row = the tester was never opted out, so the default is "notify".
            return Result<bool>.Success(subscription?.Notify ?? true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(ex.Message);
        }
    }

    public async Task<Result<IEnumerable<PassSubscription>>> GetByPassIdsAsync(IEnumerable<Guid> passIds)
    {
        try
        {
            var ids = passIds.ToList();
            var subscriptions = await _context.PassSubscriptions
                .Where(s => ids.Contains(s.PassId))
                .ToListAsync();

            return Result<IEnumerable<PassSubscription>>.Success(subscriptions);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<PassSubscription>>.Failure(ex.Message);
        }
    }

    public async Task<Result<PassSubscription>> SetNotifyAsync(Guid passId, Guid apiKeyId, bool notify)
    {
        try
        {
            var existing = await _context.PassSubscriptions
                .FirstOrDefaultAsync(s => s.PassId == passId && s.ApiKeyId == apiKeyId);

            var now = DateTimeOffset.UtcNow;

            if (existing is null)
            {
                existing = new PassSubscription
                {
                    Id = Guid.NewGuid(),
                    PassId = passId,
                    ApiKeyId = apiKeyId,
                    Notify = notify,
                    UpdatedAt = now
                };
                _context.PassSubscriptions.Add(existing);
            }
            else
            {
                existing.Notify = notify;
                existing.UpdatedAt = now;
            }

            await _context.SaveChangesAsync();
            return Result<PassSubscription>.Success(existing);
        }
        catch (Exception ex)
        {
            return Result<PassSubscription>.Failure(ex.Message);
        }
    }

    public async Task<Result> DeleteOverrideAsync(Guid passId, Guid apiKeyId)
    {
        try
        {
            var existing = await _context.PassSubscriptions
                .FirstOrDefaultAsync(s => s.PassId == passId && s.ApiKeyId == apiKeyId);

            if (existing is not null)
            {
                _context.PassSubscriptions.Remove(existing);
                await _context.SaveChangesAsync();
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> DeleteByPassIdAsync(Guid passId)
    {
        try
        {
            var rows = await _context.PassSubscriptions
                .Where(s => s.PassId == passId)
                .ToListAsync();

            if (rows.Count > 0)
            {
                _context.PassSubscriptions.RemoveRange(rows);
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
