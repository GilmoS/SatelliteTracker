using Microsoft.EntityFrameworkCore;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

// This repository manages per-tester settings (UserSettings), one row per active ApiKey.
public class UserSettingsRepository : IUserSettingsRepository
{
    private readonly AppDbContext _context;

    public UserSettingsRepository(AppDbContext context) => _context = context;

    public async Task<Result<UserSettings>> GetByApiKeyIdAsync(Guid apiKeyId)
    {
        try
        {
            var settings = await _context.UserSettings.FirstOrDefaultAsync(s => s.ApiKeyId == apiKeyId);
            return settings is null
                ? Result<UserSettings>.Failure("User settings not found.")
                : Result<UserSettings>.Success(settings);
        }
        catch (Exception ex)
        {
            return Result<UserSettings>.Failure(ex.Message);
        }
    }

    // Used by PassNotificationJob to fan out notifications to every tester whose key hasn't been revoked.
    public async Task<Result<IEnumerable<UserSettings>>> GetAllActiveAsync()
    {
        try
        {
            var settings = await _context.UserSettings
                .Include(s => s.ApiKey)
                .Where(s => s.ApiKey.IsActive)
                .ToListAsync();
            return Result<IEnumerable<UserSettings>>.Success(settings);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<UserSettings>>.Failure(ex.Message);
        }
    }

    public async Task<Result<UserSettings>> UpsertAsync(UserSettings settings)
    {
        try
        {
            var existing = await _context.UserSettings.FirstOrDefaultAsync(s => s.ApiKeyId == settings.ApiKeyId);

            if (existing is null)
            {
                if (settings.Id == Guid.Empty)
                    settings.Id = Guid.NewGuid();
                _context.UserSettings.Add(settings);
            }
            else
            {
                existing.FcmToken = settings.FcmToken;
                existing.AlertMinutes = settings.AlertMinutes;
                existing.UpdatedAt = settings.UpdatedAt;
                settings = existing;
            }

            await _context.SaveChangesAsync();
            return Result<UserSettings>.Success(settings);
        }
        catch (Exception ex)
        {
            return Result<UserSettings>.Failure(ex.Message);
        }
    }

    // Lazy-creation upserts (Milestone E, Step 1.4): AlertMinutes and FcmToken are written
    // independently, each on first write to that specific field. Unlike UpsertAsync above, these
    // never overwrite the *other* field on an existing row — that's the whole point of splitting
    // them out, see CLAUDE.md.
    public async Task<Result<UserSettings>> UpsertAlertMinutesAsync(Guid apiKeyId, int[] alertMinutes)
    {
        try
        {
            var existing = await _context.UserSettings.FirstOrDefaultAsync(s => s.ApiKeyId == apiKeyId);
            var now = DateTimeOffset.UtcNow;

            if (existing is null)
            {
                existing = new UserSettings
                {
                    Id = Guid.NewGuid(),
                    ApiKeyId = apiKeyId,
                    FcmToken = null,
                    AlertMinutes = alertMinutes,
                    UpdatedAt = now
                };
                _context.UserSettings.Add(existing);
            }
            else
            {
                existing.AlertMinutes = alertMinutes;
                existing.UpdatedAt = now;
            }

            await _context.SaveChangesAsync();
            return Result<UserSettings>.Success(existing);
        }
        catch (Exception ex)
        {
            return Result<UserSettings>.Failure(ex.Message);
        }
    }

    public async Task<Result<UserSettings>> UpsertFcmTokenAsync(Guid apiKeyId, string fcmToken)
    {
        try
        {
            var existing = await _context.UserSettings.FirstOrDefaultAsync(s => s.ApiKeyId == apiKeyId);
            var now = DateTimeOffset.UtcNow;

            if (existing is null)
            {
                existing = new UserSettings
                {
                    Id = Guid.NewGuid(),
                    ApiKeyId = apiKeyId,
                    FcmToken = fcmToken,
                    AlertMinutes = [],
                    UpdatedAt = now
                };
                _context.UserSettings.Add(existing);
            }
            else
            {
                existing.FcmToken = fcmToken;
                existing.UpdatedAt = now;
            }

            await _context.SaveChangesAsync();
            return Result<UserSettings>.Success(existing);
        }
        catch (Exception ex)
        {
            return Result<UserSettings>.Failure(ex.Message);
        }
    }
}
