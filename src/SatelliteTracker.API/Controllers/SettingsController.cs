using Microsoft.AspNetCore.Mvc;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;

namespace SatelliteTracker.API.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsRepository _repo;

    public SettingsController(ISettingsRepository repo) => _repo = repo;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _repo.GetAsync();
        if (!result.IsSuccess) return ToError(result.Error!);
        return Ok(SettingsDto.From(result.Value!));
    }

    [HttpPut]
    public async Task<IActionResult> Upsert([FromBody] UpdateSettingsRequest request)
    {
        var existing = await GetOrDefault();
        if (request.AlertMinutes is not null) existing.AlertMinutes = request.AlertMinutes;
        if (request.OutlookDays.HasValue) existing.OutlookDays = request.OutlookDays.Value;
        if (request.TeamEmail is not null) existing.TeamEmail = request.TeamEmail;
        if (request.MinElevation.HasValue) existing.MinElevation = request.MinElevation.Value;
        existing.UpdatedAt = DateTime.UtcNow;

        var result = await _repo.UpsertAsync(existing);
        if (!result.IsSuccess) return ToError(result.Error!);
        return Ok(SettingsDto.From(result.Value!));
    }

    [HttpPut("fcm-token")]
    public async Task<IActionResult> UpdateFcmToken([FromBody] UpdateFcmTokenRequest request)
    {
        var existing = await GetOrDefault();
        existing.FcmToken = request.FcmToken;
        existing.UpdatedAt = DateTime.UtcNow;

        var result = await _repo.UpsertAsync(existing);
        if (!result.IsSuccess) return ToError(result.Error!);
        return Ok(SettingsDto.From(result.Value!));
    }

    private async Task<Settings> GetOrDefault()
    {
        var result = await _repo.GetAsync();
        return result.IsSuccess
            ? result.Value!
            : new Settings
            {
                Id = Guid.Empty,
                AlertMinutes = [5, 10, 30],
                OutlookDays = 7,
                MinElevation = 5,
                UpdatedAt = DateTime.UtcNow
            };
    }

    private IActionResult ToError(string error) =>
        error.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? NotFound(new { error })
            : StatusCode(500, new { error });
}
