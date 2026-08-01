using SatelliteTracker.OutlookService.Abstractions;
using SatelliteTracker.OutlookService.Services;
using Xunit;

namespace SatelliteTracker.Tests.OutlookService;

public class IcsCalendarSyncServiceTests
{
    private readonly IcsCalendarSyncService _service = new();

    // August is Israel Daylight Time (UTC+3), so 11:00 UTC -> 14:00 local, unambiguously.
    private static CalendarEventData BuildPass(string uid = "pass-1", int orbitNumber = 1234) => new(
        Uid: uid,
        SatelliteName: "EROS C3",
        NoradId: 25544,
        OrbitNumber: orbitNumber,
        Aos: new DateTimeOffset(2026, 8, 1, 11, 0, 0, TimeSpan.Zero),
        Los: new DateTimeOffset(2026, 8, 1, 11, 10, 0, TimeSpan.Zero),
        MaxElevation: 45.3m,
        AosAzimuth: 120.5m,
        LosAzimuth: 200.1m);

    [Fact]
    public async Task SyncPassesAsync_SinglePass_ProducesValidVEvent()
    {
        var pass = BuildPass();
        var settings = new CalendarSyncSettings([10], null);

        var result = await _service.SyncPassesAsync([pass], settings);

        Assert.True(result.IsSuccess);
        var ics = result.Value!.IcsContent;

        Assert.Contains("BEGIN:VCALENDAR", ics);
        Assert.Contains("VERSION:2.0", ics);
        Assert.Contains("BEGIN:VEVENT", ics);
        Assert.Contains($"UID:{pass.Uid}", ics);
        Assert.Contains("SUMMARY:EROS C3 — Pass #1234", ics);
        Assert.Contains("DTSTART;TZID=Asia/Jerusalem:20260801T140000", ics);
        Assert.Contains("DTEND;TZID=Asia/Jerusalem:20260801T141000", ics);
        Assert.Contains("END:VEVENT", ics);
        Assert.Contains("END:VCALENDAR", ics);
        Assert.Equal([pass.Uid], result.Value.Uids);
    }

    [Fact]
    public async Task SyncPassesAsync_AlarmCountAndTriggersMatchSettings()
    {
        var pass = BuildPass();
        var settings = new CalendarSyncSettings([10, 30, 60], null);

        var result = await _service.SyncPassesAsync([pass], settings);
        var ics = result.Value!.IcsContent;

        var alarmCount = CountOccurrences(ics, "BEGIN:VALARM");
        Assert.Equal(3, alarmCount);
        Assert.Contains("TRIGGER:-PT10M", ics);
        Assert.Contains("TRIGGER:-PT30M", ics);
        Assert.Contains("TRIGGER:-PT60M", ics);
    }

    [Fact]
    public async Task SyncPassesAsync_MultiplePasses_ProducesOneVEventPerPassInSingleIcs()
    {
        var passes = new[] { BuildPass("pass-1", 1), BuildPass("pass-2", 2), BuildPass("pass-3", 3) };
        var settings = new CalendarSyncSettings([10], null);

        var result = await _service.SyncPassesAsync(passes, settings);
        var ics = result.Value!.IcsContent;

        Assert.Equal(1, CountOccurrences(ics, "BEGIN:VCALENDAR"));
        Assert.Equal(3, CountOccurrences(ics, "BEGIN:VEVENT"));
        Assert.Equal(["pass-1", "pass-2", "pass-3"], result.Value.Uids);
    }

    [Fact]
    public async Task CancelSyncedPassesAsync_ThrowsNotSupportedException()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _service.CancelSyncedPassesAsync([BuildPass()]));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
