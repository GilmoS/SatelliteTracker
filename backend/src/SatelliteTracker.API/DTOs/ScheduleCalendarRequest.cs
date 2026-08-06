namespace SatelliteTracker.API.DTOs;

/// <summary>
/// Request body for <c>POST /api/calendar/schedule</c>. Selects specific upcoming passes
/// (already shown to the user in the passes table) to bundle into a single combined .ics file.
/// </summary>
/// <param name="PassIds">IDs of the passes to include. Must contain at least one ID.</param>
/// <param name="AlertMinutes">
/// Reminder offsets (minutes before AOS) to attach to every event. Each value must be one of
/// 5, 10, 15, 30, or 60 per the product spec. An empty list is valid and means no VALARM blocks.
/// </param>
public record ScheduleCalendarRequest(IReadOnlyList<Guid> PassIds, IReadOnlyList<int> AlertMinutes)
{
    public static readonly IReadOnlyList<int> AllowedAlertMinutes = [5, 10, 15, 30, 60];
}
