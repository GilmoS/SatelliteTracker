using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SatelliteTracker.API.Authentication;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;

namespace SatelliteTracker.API.Controllers;

// This controller manages the single global application settings row:
// outlook days, team email, and minimum elevation.
// Per-tester FCM token and alert minutes now live on UserSettings and are updated
// through the authenticated per-tester settings endpoint added in a later step.
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
    // Mutating endpoint found during the Step 1.3 audit that wasn't in the originally listed set
    // — gated the same way as the other mutating endpoints rather than left anonymous.
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationOptions.SchemeName)]
    [HttpPut]
    public async Task<IActionResult> Upsert([FromBody] UpdateSettingsRequest request)
    {
        var existing = await GetOrDefault(); // Retrieve existing settings or create default settings if none exist

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

    // This helper method retrieves the current settings from the repository.
    // If no settings are found, it returns a default settings object with predefined values.
    private async Task<Settings> GetOrDefault()
    {
        var result = await _repo.GetAsync();
        return result.IsSuccess? result.Value!
            : new Settings
            {
                Id = Guid.Empty,
                OutlookDays = 7,
                MinElevation = 5,
                UpdatedAt = DateTime.UtcNow
            };
    }
}
