using Microsoft.EntityFrameworkCore;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

// This repository manages application settings, providing methods to retrieve and update settings records in the database.
public class SettingsRepository : ISettingsRepository
{
    private readonly AppDbContext _context; // The database context used to interact with the Settings table in the database.

    public SettingsRepository(AppDbContext context) => _context = context; // Constructor that initializes the repository with the provided database context.

    // Retrieves the current application settings from the database.
    public async Task<Result<Settings>> GetAsync()
    {
        try
        {
            var settings = await _context.Settings.FirstOrDefaultAsync(); // Fetch the first settings record from the database, or null if none exists.
            // If no settings record is found, return a failure result; otherwise, return a success result with the settings.
            return settings is null? Result<Settings>.Failure("Settings not found."): Result<Settings>.Success(settings);
        }
        catch (Exception ex)
        {
            return Result<Settings>.Failure(ex.Message);
        }
    }

    // Inserts a new settings record or updates the existing one in the database.
    public async Task<Result<Settings>> UpsertAsync(Settings settings)
    {
        try
        {
            var existing = await _context.Settings.FirstOrDefaultAsync();

            if (existing is null) // If no existing settings record is found, create a new one.
            {
                if (settings.Id == Guid.Empty)
                    settings.Id = Guid.NewGuid();
                _context.Settings.Add(settings);
            }
            else // If an existing settings record is found, update its properties with the new values.
            {
                existing.OutlookDays = settings.OutlookDays;
                existing.TeamEmail = settings.TeamEmail;
                existing.MinElevation = settings.MinElevation;
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
