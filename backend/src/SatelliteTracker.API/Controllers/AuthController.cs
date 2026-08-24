using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.Database.Security;

namespace SatelliteTracker.API.Controllers;

// Self-service beta registration. Not behind X-Admin-Key or any tester auth — gated only by
// the allowlist check below. See CLAUDE.md for the beta allowlist/registration flow.
[ApiController]
[Route("api/auth")]
public class AuthController : BaseController
{
    private readonly IAllowlistedEmailRepository _allowlistRepo;
    private readonly IApiKeyRepository _apiKeyRepo;

    public AuthController(IAllowlistedEmailRepository allowlistRepo, IApiKeyRepository apiKeyRepo)
    {
        _allowlistRepo = allowlistRepo;
        _apiKeyRepo = apiKeyRepo;
    }

    // POST api/auth/register
    // Does NOT create a UserSettings row — that happens later, the first time Android sends an
    // FCM token (Milestone E, Step 1.4).
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var allowlistResult = await _allowlistRepo.ExistsAsync(normalizedEmail);
        if (!allowlistResult.IsSuccess) return ToError(allowlistResult.Error!);
        if (!allowlistResult.Value)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "This email is not on the beta allowlist." });

        var existingKeyResult = await _apiKeyRepo.GetActiveByEmailAsync(normalizedEmail);
        if (existingKeyResult.IsSuccess)
        {
            return Conflict(new
            {
                error = "This email is already registered. Re-issuing a key requires admin action " +
                         "(POST /api/admin/reissue is not functional yet) — contact an admin."
            });
        }

        var rawKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            DisplayName = request.DisplayName,
            KeyHash = ApiKeyHasher.Hash(rawKey),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var createResult = await _apiKeyRepo.CreateAsync(apiKey);
        if (!createResult.IsSuccess) return ToError(createResult.Error!);

        return Ok(new RegisterResponse(rawKey, createResult.Value!.Email, createResult.Value!.DisplayName));
    }
}
