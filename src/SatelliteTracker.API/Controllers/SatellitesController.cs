using Microsoft.AspNetCore.Mvc;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;

namespace SatelliteTracker.API.Controllers;

[ApiController]
[Route("api/satellites")]
public class SatellitesController : ControllerBase
{
    private readonly ISatelliteRepository _repo;

    public SatellitesController(ISatelliteRepository repo) => _repo = repo;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _repo.GetAllAsync();
        return Respond(result, satellites => satellites.Select(SatelliteDto.From));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _repo.GetByIdAsync(id);
        return Respond(result, SatelliteDto.From);
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] CreateSatelliteRequest request)
    {
        var satellite = new Satellite
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            NoradId = request.NoradId,
            Description = request.Description,
            IsActive = true,
            IsDefault = request.IsDefault,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _repo.AddAsync(satellite);
        return Respond(result, SatelliteDto.From);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSatelliteRequest request)
    {
        var getResult = await _repo.GetByIdAsync(id);
        if (!getResult.IsSuccess)
            return ToError(getResult.Error!);

        var satellite = getResult.Value!;
        if (request.Name is not null) satellite.Name = request.Name;
        if (request.Description is not null) satellite.Description = request.Description;
        if (request.IsActive.HasValue) satellite.IsActive = request.IsActive.Value;
        if (request.IsDefault.HasValue) satellite.IsDefault = request.IsDefault.Value;

        var result = await _repo.UpdateAsync(satellite);
        return Respond(result, SatelliteDto.From);
    }

    private IActionResult Respond<TEntity, TDto>(Result<TEntity> result, Func<TEntity, TDto> map)
    {
        if (result.IsSuccess) return Ok(map(result.Value!));
        return ToError(result.Error!);
    }

    private IActionResult ToError(string error) =>
        error.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? NotFound(new { error })
            : StatusCode(500, new { error });
}
