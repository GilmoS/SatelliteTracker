using Microsoft.AspNetCore.Mvc;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.API.Filters;
using SatelliteTracker.Database.Repositories;

namespace SatelliteTracker.API.Controllers;

// BETA-ONLY ADMIN TOOLING (Milestone E, Step 1.2)
// ---------------------------------------------------------------------------------------
// These endpoints exist only to manage the beta tester allowlist by hand while there is no
// real admin auth system. They are gated by a single shared secret (X-Admin-Key — see
// RequireAdminKeyAttribute) rather than admin accounts/roles, and the allowlist is a
// manually managed DB table rather than a Sheets/Excel-backed source. This is temporary
// infrastructure for a small trusted beta group. It must be replaced with a deliberate
// redesign (real admin auth and/or an external allowlist source) before production use —
// do not extend or "productionize" this approach as-is. See CLAUDE.md.
[ApiController]
[Route("api/admin")]
[RequireAdminKey]
[ApiExplorerSettings(IgnoreApi = true)]
public class AdminController : BaseController
{
    private readonly IAllowlistedEmailRepository _allowlistRepo;

    public AdminController(IAllowlistedEmailRepository allowlistRepo) => _allowlistRepo = allowlistRepo;

    /// <summary>Beta-only admin tooling. Not present in production. See CLAUDE.md.</summary>
    // POST api/admin/allowlist
    // Idempotent: re-adding an already-allowlisted email is a no-op success, not a conflict.
    [HttpPost("allowlist")]
    public async Task<IActionResult> AddToAllowlist([FromBody] AddAllowlistEmailRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var result = await _allowlistRepo.AddAsync(normalizedEmail);
        if (!result.IsSuccess) return ToError(result.Error!);

        return Ok(AllowlistedEmailDto.From(result.Value!));
    }

    /// <summary>Beta-only admin tooling. Not present in production. See CLAUDE.md.</summary>
    // GET api/admin/allowlist
    [HttpGet("allowlist")]
    public async Task<IActionResult> GetAllowlist()
    {
        var result = await _allowlistRepo.GetAllAsync();
        if (!result.IsSuccess) return ToError(result.Error!);

        return Ok(result.Value!.Select(AllowlistedEmailDto.From));
    }

    /// <summary>Beta-only admin tooling. Not present in production. See CLAUDE.md.</summary>
    // POST api/admin/reissue
    // TODO(Milestone E, Step 1.2+): implement real re-issuance — revoke the tester's existing
    // active ApiKey and issue a new one in a single step. For now this is handled manually: an
    // admin sets the old ApiKey.IsActive = false directly in the DB, and the tester re-runs
    // POST /api/auth/register.
    [HttpPost("reissue")]
    public IActionResult Reissue([FromBody] ReissueRequest request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            error = "Key re-issuance is not implemented yet. An admin must set the tester's " +
                     "existing ApiKey.IsActive = false directly in the DB, then the tester re-runs " +
                     "POST /api/auth/register."
        });
    }
}
