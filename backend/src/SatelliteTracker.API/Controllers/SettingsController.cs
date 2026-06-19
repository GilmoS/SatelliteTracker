using Microsoft.AspNetCore.Mvc;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;

namespace SatelliteTracker.API.Controllers;

// This controller manages application settings,
// allowing clients to retrieve and update settings such as alert minutes, outlook days, team email, minimum elevation, and FCM token.
[ApiController]
[Route("api/settings")]
public class SettingsController : BaseController
{
    private readonly ISettingsRepository _repo;// Repository for settings data access

    public SettingsController(ISettingsRepository repo) => _repo = repo; // Constructor injection of the settings repository

    // GET: api/settings
    // Retrieves the current application settings.
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _repo.GetAsync();
        if (!result.IsSuccess) 
            return ToError(result.Error!);

        return Ok(SettingsDto.From(result.Value!));
    }

    // PUT: api/settings
    // Updates the application settings.
    // This method performs an upsert operation, meaning it will create new settings if they don't exist or update existing settings if they do.
    [HttpPut]
    public async Task<IActionResult> Upsert([FromBody] UpdateSettingsRequest request)
    {
        var existing = await GetOrDefault(); // Retrieve existing settings or create default settings if none exist

        if (request.AlertMinutes is not null)
            existing.AlertMinutes = request.AlertMinutes;
        if (request.OutlookDays.HasValue)
            existing.OutlookDays = request.OutlookDays.Value;
        if (request.TeamEmail is not null)
            existing.TeamEmail = request.TeamEmail;
        if (request.MinElevation.HasValue)
            existing.MinElevation = request.MinElevation.Value;

        existing.UpdatedAt = DateTime.UtcNow; // Update the timestamp to reflect the time of the update

        var result = await _repo.UpsertAsync(existing); // Perform the upsert operation
        if (!result.IsSuccess)
            return ToError(result.Error!);

        return Ok(SettingsDto.From(result.Value!));
    }

    // PUT: api/settings/fcm-token
    // Updates the FCM token in the application settings.
    // This method is separate from the general settings update to allow for more focused updates to the FCM token,
    // which may be updated more frequently than other settings.
    [HttpPut("fcm-token")]
    public async Task<IActionResult> UpdateFcmToken([FromBody] UpdateFcmTokenRequest request)
    {
        var existing = await GetOrDefault(); // Retrieve existing settings or create default settings if none exist
        existing.FcmToken = request.FcmToken; // Update the FCM token with the new value from the request
        existing.UpdatedAt = DateTime.UtcNow; 

        var result = await _repo.UpsertAsync(existing);
        if (!result.IsSuccess)
            return ToError(result.Error!);

        return Ok(SettingsDto.From(result.Value!));
    }


    // This helper method retrieves the current settings from the repository.
    // If no settings are found, it returns a default settings object with predefined values.
    private async Task<Settings> GetOrDefault()
    {
        var result = await _repo.GetAsync();
        return result.IsSuccess? result.Value!
            : new Settings
            {
                Id = Guid.Empty,
                AlertMinutes = [5, 10, 30],
                OutlookDays = 7,
                MinElevation = 5,
                UpdatedAt = DateTime.UtcNow
            };
    }

    
    
}
