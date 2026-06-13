using Microsoft.AspNetCore.Mvc;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.TLEService.Services;

namespace SatelliteTracker.API.Controllers;

[ApiController]
[Route("api/tles")]
public class TlesController : ControllerBase
{
    private readonly ISatelliteRepository _satelliteRepo;
    private readonly ITleRepository _tleRepo;
    private readonly ITleService _tleService;

    public TlesController(
        ISatelliteRepository satelliteRepo,
        ITleRepository tleRepo,
        ITleService tleService)
    {
        _satelliteRepo = satelliteRepo;
        _tleRepo = tleRepo;
        _tleService = tleService;
    }

    [HttpGet("{satelliteId:guid}")]
    public async Task<IActionResult> GetLatest(Guid satelliteId)
    {
        var satResult = await _satelliteRepo.GetByIdAsync(satelliteId);
        if (!satResult.IsSuccess) return ToError(satResult.Error!);

        var result = await _tleService.GetLatestAsync(satResult.Value!.NoradId);
        if (!result.IsSuccess) return ToError(result.Error!);
        return Ok(TleDto.From(result.Value!));
    }

    [HttpGet("{satelliteId:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid satelliteId)
    {
        var satResult = await _satelliteRepo.GetByIdAsync(satelliteId);
        if (!satResult.IsSuccess) return ToError(satResult.Error!);

        var result = await _tleRepo.GetHistoryAsync(
            satResult.Value!.NoradId, DateTime.UtcNow.AddMonths(-6));
        if (!result.IsSuccess) return ToError(result.Error!);
        return Ok(result.Value!.Select(TleDto.From));
    }

    [HttpPost("{satelliteId:guid}/fetch")]
    public async Task<IActionResult> Fetch(Guid satelliteId, CancellationToken ct)
    {
        var satResult = await _satelliteRepo.GetByIdAsync(satelliteId);
        if (!satResult.IsSuccess) return ToError(satResult.Error!);

        var result = await _tleService.FetchAndSaveAsync(satResult.Value!.NoradId, ct);
        if (!result.IsSuccess) return ToError(result.Error!);
        return Ok(TleDto.From(result.Value!));
    }

    private IActionResult ToError(string error) =>
        error.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? NotFound(new { error })
            : StatusCode(500, new { error });
}
