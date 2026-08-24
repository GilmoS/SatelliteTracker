using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.TLEService.Client;

namespace SatelliteTracker.API.Controllers;

// This controller provides real-time satellite data, including current position and recent track history.
[ApiController]
[Route("api/satellites")]
public class RealTimeController : BaseController
{


    private readonly ISatelliteRepository _satelliteRepo; // Repository for accessing satellite data from the database
    private readonly IN2YOClient _n2yo; // Client for interacting with the N2YO API to retrieve real-time satellite data
    private readonly IMemoryCache _cache; // In-memory cache for storing recent satellite position and track data to reduce API calls and improve performance
    private readonly ObserverSettings _observer;



    // Constructor that initializes the satellite repository, N2YO client, and memory cache through dependency injection.
    public RealTimeController(ISatelliteRepository satelliteRepo, IN2YOClient n2yo, IMemoryCache cache , IOptions<ObserverSettings> observer)
    {
        _satelliteRepo = satelliteRepo;
        _n2yo = n2yo;
        _cache = cache;
        _observer = observer.Value;
    }
    

    // GET api/satellites/{id}/position - Retrieves the current position of a satellite by its ID
    [HttpGet("{id:guid}/position")]
    [ProducesResponseType(typeof(PositionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPosition(Guid id, CancellationToken ct)
    {
        var satResult = await _satelliteRepo.GetByIdAsync(id);

        if (!satResult.IsSuccess)
            return ToError(satResult.Error!);

        var noradId = satResult.Value!.NoradId;

        var cacheKey = $"pos:{noradId}";
        if (_cache.TryGetValue(cacheKey, out PositionDto? cached))
            return Ok(cached);

        // Call the N2YO API to get the current position of the satellite. We request only 1 second of data since we only need the latest position.
        var result = await _n2yo.GetPositionsAsync(noradId, _observer.Lat, _observer.Lng, _observer.AltMeters, seconds: 1, ct);
        if (!result.IsSuccess)
            return ToError(result.Error!);

        // Ensure we have position data returned from the API. If not, return a 500 error indicating that no position data was available.
        var positions = result.Value!.Positions;
        if (positions.Length == 0)
            return StatusCode(500, new { error = "No position data returned." });

        // Create a PositionDto from the first (and only) position returned by the API, cache it for 30 seconds, and return it in the response.
        var dto = PositionDto.From(noradId, result.Value!.Info.SatName, positions[0]);
        _cache.Set(cacheKey, dto, TimeSpan.FromSeconds(30));
        return Ok(dto);
    }

    // GET api/satellites/{id}/track - Retrieves the recent track history of a satellite by its ID
    [HttpGet("{id:guid}/track")]
    [ProducesResponseType(typeof(TrackDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTrack(Guid id, CancellationToken ct)
    {
        var satResult = await _satelliteRepo.GetByIdAsync(id);
        if (!satResult.IsSuccess) return ToError(satResult.Error!);
        var noradId = satResult.Value!.NoradId;

        var cacheKey = $"track:{noradId}";
        if (_cache.TryGetValue(cacheKey, out TrackDto? cached))
            return Ok(cached);

        var result = await _n2yo.GetPositionsAsync(noradId, _observer.Lat, _observer.Lng, _observer.AltMeters, seconds: 300, ct);
        if (!result.IsSuccess)
            return ToError(result.Error!);

        var dto = new TrackDto
        {
            NoradId = noradId,
            SatName = result.Value!.Info.SatName,
            Points = result.Value!.Positions.Select(TrackPointDto.From).ToList()
        };
        _cache.Set(cacheKey, dto, TimeSpan.FromMinutes(5));
        return Ok(dto);
    }

}
