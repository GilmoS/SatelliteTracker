using Microsoft.EntityFrameworkCore;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

public class SettingsRepository : ISettingsRepository
{
    private readonly AppDbContext _context;

    public SettingsRepository(AppDbContext context) => _context = context;

    public async Task<Result<Settings>> GetAsync()
    {
        try
        {
            var settings = await _context.Settings.FirstOrDefaultAsync();
            return settings is null
                ? Result<Settings>.Failure("Settings not found.")
                : Result<Settings>.Success(settings);
        }
        catch (Exception ex)
        {
            return Result<Settings>.Failure(ex.Message);
        }
    }

    public async Task<Result<Settings>> UpsertAsync(Settings settings)
    {
        try
        {
            var existing = await _context.Settings.FirstOrDefaultAsync();
            if (existing is null)
            {
                if (settings.Id == Guid.Empty)
                    settings.Id = Guid.NewGuid();
                _context.Settings.Add(settings);
            }
            else
            {
                existing.AlertMinutes = settings.AlertMinutes;
                existing.OutlookDays = settings.OutlookDays;
                existing.TeamEmail = settings.TeamEmail;
                existing.MinElevation = settings.MinElevation;
                existing.FcmToken = settings.FcmToken;
                existing.UpdatedAt = settings.UpdatedAt;
                settings = existing;
            }

            await _context.SaveChangesAsync();
            return Result<Settings>.Success(settings);
        }
        catch (Exception ex)
        {
            return Result<Settings>.Failure(ex.Message);
        }
    }
}
