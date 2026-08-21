using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SatelliteTracker.API.Authentication;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.TLEService.Services;

namespace SatelliteTracker.API.Controllers;

//This controller manages TLE data for satellites, including fetching the latest TLE, retrieving historical TLEs, and fetching/saving new TLE data.
//It interacts with satellite and TLE repositories and a TLE service to perform these operations.
//Error handling is implemented to return appropriate HTTP status codes based on the nature of the error (e.g., not found vs. internal server error).
[ApiController]
[Route("api/tles")]
public class TlesController : BaseController
{
    private readonly ISatelliteRepository _satelliteRepo; // Repository for accessing satellite data
    private readonly ITleRepository _tleRepo; // Repository for accessing TLE data
    private readonly ITleService _tleService; // Service for handling TLE-related operations, such as fetching and saving TLE data

    // Constructor injection of the  TLE Repository
    public TlesController(ISatelliteRepository satelliteRepo,ITleRepository tleRepo,ITleService tleService)
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

    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationOptions.SchemeName)]
    [HttpPost("{satelliteId:guid}/fetch")]
    public async Task<IActionResult> Fetch(Guid satelliteId, CancellationToken ct)
    {
        var satResult = await _satelliteRepo.GetByIdAsync(satelliteId);
        if (!satResult.IsSuccess) return ToError(satResult.Error!);

        var result = await _tleService.FetchAndSaveAsync(satResult.Value!.NoradId, ct);
        if (!result.IsSuccess) return ToError(result.Error!);
        return Ok(TleDto.From(result.Value!));
    }

    
}
