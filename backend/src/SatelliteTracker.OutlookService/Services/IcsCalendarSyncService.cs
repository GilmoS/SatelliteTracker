using System.Text;
using SatelliteTracker.Database.Common;
using SatelliteTracker.OutlookService.Abstractions;

namespace SatelliteTracker.OutlookService.Services;

/// <summary>
/// ICS-file based implementation of <see cref="ICalendarSyncService"/>. Builds a single combined
/// RFC 5545 .ics document with one VEVENT per pass; some other component is responsible for
/// delivering the resulting file to the user, who imports it into their calendar manually.
/// </summary>
public class IcsCalendarSyncService : ICalendarSyncService
{
    private const string TimeZoneId = "Asia/Jerusalem";
    private static readonly TimeZoneInfo IsraelTimeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    /// <summary>
    /// Builds one VEVENT per pass (with a VALARM per configured alert offset) inside a single
    /// combined .ics document.
    /// </summary>
    public Task<Result<CalendarSyncOutcome>> SyncPassesAsync(
        IReadOnlyList<CalendarEventData> passes,
        CalendarSyncSettings settings,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        AppendLine(sb, "BEGIN:VCALENDAR");
        AppendLine(sb, "VERSION:2.0");
        AppendLine(sb, "PRODID:-//SatelliteTracker//Calendar Sync//EN");
        AppendLine(sb, "CALSCALE:GREGORIAN");
        AppendVTimeZone(sb);

        var uids = new List<string>(passes.Count);
        foreach (var pass in passes)
        {
            AppendVEvent(sb, pass, settings);
            uids.Add(pass.Uid);
        }

        AppendLine(sb, "END:VCALENDAR");

        var outcome = new CalendarSyncOutcome(sb.ToString(), uids);
        return Task.FromResult(Result<CalendarSyncOutcome>.Success(outcome));
    }

    /// <summary>
    /// Not supported: the ICS model never has programmatic access to the user's calendar in the
    /// first place — events are added manually by the user, and per the product spec there is no
    /// requirement to cancel a scheduled notification/event from the app. Manual removal by the
    /// user is sufficient. This is a deliberate, permanent design boundary, not a pending TODO.
    /// </summary>
    public Task<Result> CancelSyncedPassesAsync(
        IReadOnlyList<CalendarEventData> passes,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "ICS calendar sync has no programmatic access to the user's calendar. Events are added " +
            "manually by the user; per the product spec there is no requirement to cancel a scheduled " +
            "event from the app, so manual removal by the user is sufficient.");
    }

    private static void AppendVTimeZone(StringBuilder sb)
    {
        // Standard IANA-style VTIMEZONE block for Asia/Jerusalem (Israel's post-2013 fixed DST rule).
        AppendLine(sb, "BEGIN:VTIMEZONE");
        AppendLine(sb, $"TZID:{TimeZoneId}");
        AppendLine(sb, "BEGIN:STANDARD");
        AppendLine(sb, "DTSTART:19700101T020000");
        AppendLine(sb, "TZOFFSETFROM:+0300");
        AppendLine(sb, "TZOFFSETTO:+0200");
        AppendLine(sb, "RRULE:FREQ=YEARLY;BYMONTH=10;BYDAY=-1SU");
        AppendLine(sb, "TZNAME:IST");
        AppendLine(sb, "END:STANDARD");
        AppendLine(sb, "BEGIN:DAYLIGHT");
        AppendLine(sb, "DTSTART:19700101T020000");
        AppendLine(sb, "TZOFFSETFROM:+0200");
        AppendLine(sb, "TZOFFSETTO:+0300");
        AppendLine(sb, "RRULE:FREQ=YEARLY;BYMONTH=3;BYDAY=-1FR");
        AppendLine(sb, "TZNAME:IDT");
        AppendLine(sb, "END:DAYLIGHT");
        AppendLine(sb, "END:VTIMEZONE");
    }

    private static void AppendVEvent(StringBuilder sb, CalendarEventData pass, CalendarSyncSettings settings)
    {
        var aosLocal = TimeZoneInfo.ConvertTime(pass.Aos, IsraelTimeZone);
        var losLocal = TimeZoneInfo.ConvertTime(pass.Los, IsraelTimeZone);

        AppendLine(sb, "BEGIN:VEVENT");
        AppendLine(sb, $"UID:{pass.Uid}");
        AppendLine(sb, $"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmss}Z");
        AppendLine(sb, $"DTSTART;TZID={TimeZoneId}:{aosLocal:yyyyMMddTHHmmss}");
        AppendLine(sb, $"DTEND;TZID={TimeZoneId}:{losLocal:yyyyMMddTHHmmss}");
        AppendLine(sb, $"SUMMARY:{EscapeText($"{pass.SatelliteName} — Pass #{pass.OrbitNumber}")}");
        AppendLine(sb, $"DESCRIPTION:{EscapeText(BuildDescription(pass, aosLocal, losLocal))}");

        foreach (var minutes in settings.AlertMinutes)
        {
            AppendLine(sb, "BEGIN:VALARM");
            AppendLine(sb, $"TRIGGER:-PT{minutes}M");
            AppendLine(sb, "ACTION:DISPLAY");
            AppendLine(sb, $"DESCRIPTION:{EscapeText($"{pass.SatelliteName} pass in {minutes} minutes")}");
            AppendLine(sb, "END:VALARM");
        }

        AppendLine(sb, "END:VEVENT");
    }

    private static string BuildDescription(CalendarEventData pass, DateTimeOffset aosLocal, DateTimeOffset losLocal)
    {
        return string.Join('\n',
            $"Max Elevation: {pass.MaxElevation}°",
            $"AOS Azimuth: {pass.AosAzimuth}° | LOS Azimuth: {pass.LosAzimuth}°",
            $"AOS (IL): {aosLocal:yyyy-MM-dd HH:mm} | AOS (UTC): {pass.Aos.UtcDateTime:yyyy-MM-dd HH:mm}",
            $"LOS (IL): {losLocal:yyyy-MM-dd HH:mm} | LOS (UTC): {pass.Los.UtcDateTime:yyyy-MM-dd HH:mm}");
    }

    private static string EscapeText(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n");
    }

    private static void AppendLine(StringBuilder sb, string content)
    {
        sb.Append(Fold(content));
        sb.Append("\r\n");
    }

    // RFC 5545 line folding: lines longer than 75 octets are split with CRLF + a leading space.
    private static string Fold(string line)
    {
        const int maxLength = 75;
        if (line.Length <= maxLength)
        {
            return line;
        }

        var sb = new StringBuilder();
        var remaining = line;
        var first = true;
        while (remaining.Length > 0)
        {
            var chunkLength = first ? maxLength : maxLength - 1;
            if (!first)
            {
                sb.Append("\r\n ");
            }

            var take = Math.Min(chunkLength, remaining.Length);
            sb.Append(remaining[..take]);
            remaining = remaining[take..];
            first = false;
        }

        return sb.ToString();
    }
}
