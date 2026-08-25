using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SatelliteTracker.API.Authentication;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.Database.Repositories;

namespace SatelliteTracker.API.Controllers;

// Per-tester notification settings (FcmToken, AlertMinutes) — Milestone E, Step 1.4.
// This is the first inherently-tester-specific GET in the API, and thus the first exception to
// the "all GETs are anonymous" rule from Step 1.3 (see CLAUDE.md): the response depends on which
// tester is asking, so it must be [Authorize]d like the mutating endpoints.
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationOptions.SchemeName)]
[ApiController]
[Route("api/settings/me")]
public class SettingsMeController : BaseController
{
    // Per the spec (CLAUDE.md), not derived from any other data.
    private static readonly int[] ValidAlertMinutes = [5, 10, 15, 30, 60];

    private readonly IUserSettingsRepository _repo;

    public SettingsMeController(IUserSettingsRepository repo) => _repo = repo;

    // GET api/settings/me
    // Read-only in all cases. If no UserSettings row exists for this tester yet, returns a
    // computed default (AlertMinutes: [], FcmToken: null) — it never creates a row as a side
    // effect of reading.
    [HttpGet]
    [ProducesResponseType(typeof(UserSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get()
    {
        var apiKeyId = User.GetApiKeyId();
        var result = await _repo.GetByApiKeyIdAsync(apiKeyId);

        return Ok(result.IsSuccess ? UserSettingsDto.From(result.Value!) : UserSettingsDto.Default);
    }

    // PUT api/settings/me
    // Upserts AlertMinutes only. On an existing row, FcmToken is left untouched — see
    // IUserSettingsRepository.UpsertAlertMinutesAsync.
    [HttpPut]
    [ProducesResponseType(typeof(UserSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateAlertMinutes([FromBody] UpdateAlertMinutesRequest request)
    {
        if (request.AlertMinutes.Any(m => !ValidAlertMinutes.Contains(m)))
        {
            return BadRequest(new
            {
                error = $"AlertMinutes values must be drawn from [{string.Join(", ", ValidAlertMinutes)}]."
            });
        }

        var apiKeyId = User.GetApiKeyId();
        var result = await _repo.UpsertAlertMinutesAsync(apiKeyId, request.AlertMinutes);
        if (!result.IsSuccess) return ToError(result.Error!);

        return Ok(UserSettingsDto.From(result.Value!));
    }

    // PUT api/settings/me/fcm-token
    // Upserts FcmToken only. On an existing row, AlertMinutes is left untouched — see
    // IUserSettingsRepository.UpsertFcmTokenAsync.
    [HttpPut("fcm-token")]
    [ProducesResponseType(typeof(UserSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateFcmToken([FromBody] UpdateFcmTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FcmToken))
            return BadRequest(new { error = "FcmToken must not be empty." });

        var apiKeyId = User.GetApiKeyId();
        var result = await _repo.UpsertFcmTokenAsync(apiKeyId, request.FcmToken);
        if (!result.IsSuccess) return ToError(result.Error!);

        return Ok(UserSettingsDto.From(result.Value!));
    }
}
