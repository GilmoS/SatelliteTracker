using Microsoft.AspNetCore.Mvc;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.PassService.Services;

namespace SatelliteTracker.API.Controllers;

[ApiController]
[Route("api/passes")]
public class PassesController : ControllerBase
{
    private readonly IPassService _passService;

    public PassesController(IPassService passService) => _passService = passService;

    [HttpGet("{satelliteId:guid}")]
    public async Task<IActionResult> GetUpcoming(Guid satelliteId)
    {
        var result = await _passService.GetUpcomingPassesAsync(satelliteId);
        if (!result.IsSuccess) return ToError(result.Error!);
        return Ok(result.Value!.Select(PassDto.From));
    }

    [HttpGet("{satelliteId:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid satelliteId)
    {
        var result = await _passService.GetPassHistoryAsync(satelliteId);
        if (!result.IsSuccess) return ToError(result.Error!);
        return Ok(result.Value!.Select(PassDto.From));
    }

    [HttpGet("pass/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _passService.GetPassByIdAsync(id);
        if (!result.IsSuccess) return ToError(result.Error!);
        return Ok(PassDto.From(result.Value!));
    }

    private IActionResult ToError(string error) =>
        error.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? NotFound(new { error })
            : StatusCode(500, new { error });
}
