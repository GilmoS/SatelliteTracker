using Microsoft.AspNetCore.Mvc;
using SatelliteTracker.API.DTOs;
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
    private readonly IPassRepository _passRepository;

    public PassesController(IPassService passService, IPassRepository passRepository)
    {
        _passService = passService;
        _passRepository = passRepository;
    }


    // GET api/passes/{satelliteId}
    [HttpGet("{satelliteId:guid}")]
    public async Task<IActionResult> GetUpcoming(Guid satelliteId)
    {
        var result = await _passService.GetUpcomingPassesAsync(satelliteId);
        if (!result.IsSuccess) return ToError(result.Error!);
        return Ok(result.Value!.Select(PassDto.From));
    }


    // GET api/passes/{satelliteId}/history
    [HttpGet("{satelliteId:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid satelliteId)
    {
        var result = await _passService.GetPassHistoryAsync(satelliteId);
        if (!result.IsSuccess) return ToError(result.Error!);
        return Ok(result.Value!.Select(PassDto.From));
    }


    // GET api/passes/pass/{id}
    [HttpGet("pass/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _passService.GetPassByIdAsync(id);
        if (!result.IsSuccess) return ToError(result.Error!);
        return Ok(PassDto.From(result.Value!));
    }


    // PATCH api/passes/{id}/notify
    [HttpPatch("{id:guid}/notify")]
    public async Task<IActionResult> PatchNotify(Guid id, [FromBody] PatchNotifyRequest request)
    {
        var result = await _passRepository.UpdateNotifyAsync(id, request.Notify);
        if (!result.IsSuccess) return ToError(result.Error!);
        return Ok(PassDto.From(result.Value!));
    }
}
