using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.TLEService.Client;

namespace SatelliteTracker.API.Controllers;

[ApiController]
[Route("api/satellites")]
public class RealTimeController : ControllerBase
{
    private const double ObserverLat = 32.0055;
    private const double ObserverLng = 34.8854;
    private const double ObserverAltM = 135;

    private readonly ISatelliteRepository _satelliteRepo;
    private readonly IN2YOClient _n2yo;
    private readonly IMemoryCache _cache;

    public RealTimeController(
        ISatelliteRepository satelliteRepo,
        IN2YOClient n2yo,
        IMemoryCache cache)
    {
        _satelliteRepo = satelliteRepo;
        _n2yo = n2yo;
        _cache = cache;
    }

    [HttpGet("{id:guid}/position")]
    public async Task<IActionResult> GetPosition(Guid id, CancellationToken ct)
    {
        var satResult = await _satelliteRepo.GetByIdAsync(id);
        if (!satResult.IsSuccess) return ToError(satResult.Error!);
        var noradId = satResult.Value!.NoradId;

        var cacheKey = $"pos:{noradId}";
        if (_cache.TryGetValue(cacheKey, out PositionDto? cached))
            return Ok(cached);

        var result = await _n2yo.GetPositionsAsync(
            noradId, ObserverLat, ObserverLng, ObserverAltM, seconds: 1, ct);
        if (!result.IsSuccess) return StatusCode(500, new { error = result.Error });

        var positions = result.Value!.Positions;
        if (positions.Length == 0)
            return StatusCode(500, new { error = "No position data returned." });

        var dto = PositionDto.From(noradId, result.Value!.Info.SatName, positions[0]);
        _cache.Set(cacheKey, dto, TimeSpan.FromSeconds(30));
        return Ok(dto);
    }

    [HttpGet("{id:guid}/track")]
    public async Task<IActionResult> GetTrack(Guid id, CancellationToken ct)
    {
        var satResult = await _satelliteRepo.GetByIdAsync(id);
        if (!satResult.IsSuccess) return ToError(satResult.Error!);
        var noradId = satResult.Value!.NoradId;

        var cacheKey = $"track:{noradId}";
        if (_cache.TryGetValue(cacheKey, out TrackDto? cached))
            return Ok(cached);

        var result = await _n2yo.GetPositionsAsync(
            noradId, ObserverLat, ObserverLng, ObserverAltM, seconds: 300, ct);
        if (!result.IsSuccess) return StatusCode(500, new { error = result.Error });

        var dto = new TrackDto
        {
            NoradId = noradId,
            SatName = result.Value!.Info.SatName,
            Points = result.Value!.Positions.Select(TrackPointDto.From).ToList()
        };
        _cache.Set(cacheKey, dto, TimeSpan.FromMinutes(5));
        return Ok(dto);
    }

    private IActionResult ToError(string error) =>
        error.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? NotFound(new { error })
            : StatusCode(500, new { error });
}
