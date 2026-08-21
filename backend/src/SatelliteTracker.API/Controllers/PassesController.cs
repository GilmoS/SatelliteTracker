using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SatelliteTracker.API.Authentication;
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
    private readonly IPassSubscriptionRepository _subscriptionRepo;

    public PassesController(IPassService passService, IPassSubscriptionRepository subscriptionRepo)
    {
        _passService = passService;
        _subscriptionRepo = subscriptionRepo;
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
    // Upserts the calling tester's PassSubscription opt-out row. Notify = true is the sparse
    // default (see IPassSubscriptionRepository), so setting it back to true deletes any existing
    // override row instead of writing a redundant "true" row.
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationOptions.SchemeName)]
    [HttpPatch("{id:guid}/notify")]
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

        return Ok(new { passId = id, notify = effectiveResult.Value });
    }
}
