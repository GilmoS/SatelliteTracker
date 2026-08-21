using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SatelliteTracker.API.Authentication;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.OutlookService.Abstractions;
using SatelliteTracker.OutlookService.Mapping;

namespace SatelliteTracker.API.Controllers;

/// <summary>
/// Production entry point for calendar sync (ICS MVP). The user selects one or more upcoming
/// passes from the passes table already shown in the app plus their chosen alert minutes; this
/// returns a single combined .ics covering exactly those passes. The Android app opens the
/// result via ACTION_VIEW to add it to the user's own calendar, and can ACTION_SEND it to share
/// with a team lead — there is no backend email-sending and no Microsoft Graph in this flow.
/// Replaces the removed DevCalendarTestController.
/// </summary>
[ApiController]
[Route("api/calendar")]
public class CalendarController : BaseController
{
    private readonly IPassRepository _passRepository;
    private readonly ICalendarSyncService _calendarSyncService;

    public CalendarController(IPassRepository passRepository, ICalendarSyncService calendarSyncService)
    {
        _passRepository = passRepository;
        _calendarSyncService = calendarSyncService;
    }

    // POST api/calendar/schedule
    // Doesn't write to the DB beyond MarkOutlookSyncedAsync, but must still require a known
    // tester — an anonymous, unauthenticated calendar-generation endpoint is an abuse/DoS surface.
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationOptions.SchemeName)]
    [HttpPost("schedule")]
    public async Task<IActionResult> Schedule([FromBody] ScheduleCalendarRequest request)
    {
        if (request.PassIds is null || request.PassIds.Count == 0)
            return BadRequest(new { error = "PassIds must contain at least one pass ID." });

        var alertMinutes = request.AlertMinutes ?? [];
        var invalidAlertMinutes = alertMinutes
            .Where(m => !ScheduleCalendarRequest.AllowedAlertMinutes.Contains(m))
            .Distinct()
            .ToList();
        if (invalidAlertMinutes.Count > 0)
        {
            return BadRequest(new
            {
                error = $"Invalid AlertMinutes value(s): {string.Join(", ", invalidAlertMinutes)}. " +
                         $"Allowed values are: {string.Join(", ", ScheduleCalendarRequest.AllowedAlertMinutes)}."
            });
        }

        var passesResult = await _passRepository.GetByIdsAsync(request.PassIds);
        if (!passesResult.IsSuccess) return ToError(passesResult.Error!);

        var eventData = passesResult.Value!.Select(p => p.ToCalendarEventData()).ToList();

        // TeamEmail is reserved for a future Graph implementation; the ICS model has no
        // programmatic way to notify a third party, so sharing happens on-device via
        // ACTION_SEND after the user receives the .ics.
        var settings = new CalendarSyncSettings(AlertMinutes: alertMinutes, TeamEmail: null);

        var syncResult = await _calendarSyncService.SyncPassesAsync(eventData, settings);
        if (!syncResult.IsSuccess) return ToError(syncResult.Error!);

        var markResult = await _passRepository.MarkOutlookSyncedAsync(request.PassIds);
        if (!markResult.IsSuccess) return ToError(markResult.Error!);

        var bytes = System.Text.Encoding.UTF8.GetBytes(syncResult.Value!.IcsContent);
        return File(bytes, "text/calendar; charset=utf-8", "sattrakk-passes.ics");
    }
}
