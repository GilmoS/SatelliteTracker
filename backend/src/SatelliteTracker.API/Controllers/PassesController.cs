using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SatelliteTracker.API.Authentication;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.PassService.Services;

namespace SatelliteTracker.API.Controllers;

// This controller manages satellite passes.
// It provides endpoints to retrieve upcoming passes, pass history, and specific pass details by ID.
[ApiController]
[Route("api/passes")]
public class PassesController : BaseController
{
    private readonly IPassService _passService;
    private readonly IPassSubscriptionRepository _subscriptionRepo;
    private readonly IMemoryCache _cache;

    public PassesController(IPassService passService, IPassSubscriptionRepository subscriptionRepo, IMemoryCache cache)
    {
        _passService = passService;
        _subscriptionRepo = subscriptionRepo;
        _cache = cache;
    }


    // GET api/passes/{satelliteId}
    [HttpGet("{satelliteId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<PassDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUpcoming(Guid satelliteId)
    {
        var result = await _passService.GetUpcomingPassesAsync(satelliteId);
        if (!result.IsSuccess) return ToError(result.Error!);
        return Ok(result.Value!.Select(PassDto.From));
    }


    // GET api/passes/{satelliteId}/history
    // Paginated + filterable, unlike GetUpcoming above — see CLAUDE.md's Pass History section for
    // why this endpoint returns a PagedResultDto envelope instead of a bare array. All filters are
    // optional and independently AND-combined; sort order is fixed (Aos descending) and not
    // configurable. hasMore is computed by the repository fetching pageSize+1 rows.
    private const int MaxPageSize = 200;

    [HttpGet("{satelliteId:guid}/history")]
    [ProducesResponseType(typeof(PagedResultDto<PassDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetHistory(
        Guid satelliteId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] int? orbitNumberFrom = null,
        [FromQuery] int? orbitNumberTo = null,
        [FromQuery] decimal? maxElevationFrom = null,
        [FromQuery] decimal? maxElevationTo = null,
        [FromQuery] DateTime? aosFrom = null,
        [FromQuery] DateTime? aosTo = null,
        [FromQuery] DateTime? losFrom = null,
        [FromQuery] DateTime? losTo = null)
    {
        if (page < 1)
            return BadRequest(new { error = "page must be at least 1." });
        if (pageSize < 1 || pageSize > MaxPageSize)
            return BadRequest(new { error = $"pageSize must be between 1 and {MaxPageSize}." });

        var query = new PassHistoryQuery
        {
            Page = page,
            PageSize = pageSize,
            OrbitNumberFrom = orbitNumberFrom,
            OrbitNumberTo = orbitNumberTo,
            MaxElevationFrom = maxElevationFrom,
            MaxElevationTo = maxElevationTo,
            AosFrom = aosFrom,
            AosTo = aosTo,
            LosFrom = losFrom,
            LosTo = losTo
        };

        var result = await _passService.GetPassHistoryAsync(satelliteId, query);
        if (!result.IsSuccess) return ToError(result.Error!);

        var paged = result.Value!;
        return Ok(new PagedResultDto<PassDto>(
            paged.Items.Select(PassDto.From).ToList(), paged.Page, paged.PageSize, paged.HasMore));
    }


    // GET api/passes/pass/{id}
    [HttpGet("pass/{id:guid}")]
    [ProducesResponseType(typeof(PassDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _passService.GetPassByIdAsync(id);
        if (!result.IsSuccess) return ToError(result.Error!);
        return Ok(PassDto.From(result.Value!));
    }


    // GET api/passes/{id}/track
    // Ground track for this specific, already-calculated pass — anchored to its stored TleId and
    // fixed [Aos, Los] window. Distinct from RealTimeController.GetTrack (api/satellites/{id}/track),
    // which is a live, "now"-anchored track pulled from N2YO off the satellite's current TLE. This
    // result is deterministic per passId (same TleId, same fixed window), so it's cached for 1 hour
    // rather than 5 minutes, and keyed by passId alone.
    [HttpGet("{id:guid}/track")]
    [ProducesResponseType(typeof(PassTrackDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTrack(Guid id)
    {
        var cacheKey = $"pass-track:{id}";
        if (_cache.TryGetValue(cacheKey, out PassTrackDto? cached))
            return Ok(cached);

        var result = await _passService.GetPassTrackAsync(id);
        if (!result.IsSuccess) return ToError(result.Error!);

        var dto = new PassTrackDto
        {
            PassId = id,
            Points = result.Value!.Select(PassTrackPointDto.From).ToList()
        };

        _cache.Set(cacheKey, dto, TimeSpan.FromHours(1));
        return Ok(dto);
    }


    // PATCH api/passes/{id}/notify
    // Upserts the calling tester's PassSubscription opt-out row. Notify = true is the sparse
    // default (see IPassSubscriptionRepository), so setting it back to true deletes any existing
    // override row instead of writing a redundant "true" row.
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationOptions.SchemeName)]
    [HttpPatch("{id:guid}/notify")]
    [ProducesResponseType(typeof(NotifyStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PatchNotify(Guid id, [FromBody] PatchNotifyRequest request)
    {
        var passResult = await _passService.GetPassByIdAsync(id);
        if (!passResult.IsSuccess) return ToError(passResult.Error!);

        var apiKeyId = User.GetApiKeyId();

        if (request.Notify)
        {
            var deleteResult = await _subscriptionRepo.DeleteOverrideAsync(id, apiKeyId);
            if (!deleteResult.IsSuccess) return ToError(deleteResult.Error!);
        }
        else
        {
            var setResult = await _subscriptionRepo.SetNotifyAsync(id, apiKeyId, notify: false);
            if (!setResult.IsSuccess) return ToError(setResult.Error!);
        }

        var effectiveResult = await _subscriptionRepo.GetEffectiveNotifyStatusAsync(id, apiKeyId);
        if (!effectiveResult.IsSuccess) return ToError(effectiveResult.Error!);

        return Ok(new NotifyStatusDto(id, effectiveResult.Value));
    }
}
