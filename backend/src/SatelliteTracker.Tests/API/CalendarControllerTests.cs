using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using SatelliteTracker.API.Controllers;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.Database;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.OutlookService.Services;
using SatelliteTracker.Tests.Database.Helpers;
using Xunit;

namespace SatelliteTracker.Tests.API;

// Integration-style: exercises the real PassRepository (SQLite in-memory) and the real
// IcsCalendarSyncService together, since the value of these tests is in the full round trip
// (request -> .ics content -> OutlookSynced persisted), not in mocking either collaborator.
public class CalendarControllerTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly PassRepository _passRepository;
    private readonly CalendarController _controller;

    public CalendarControllerTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _passRepository = new PassRepository(_context);
        _controller = new CalendarController(_passRepository, new IcsCalendarSyncService());
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private (Satellite sat, TleRecord tle) Seed()
    {
        var sat = new Satellite
        {
            Id = Guid.NewGuid(),
            Name = "EROS C3",
            NoradId = 54880,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var tle = new TleRecord
        {
            Id = Guid.NewGuid(),
            SatelliteId = sat.Id,
            Line1 = "1 54880U 22099A   23001.00000000  .00000000  00000-0  00000-0 0  9999",
            Line2 = "2 54880  97.7000 208.9163 0001382  95.6617 344.7248 14.98120000303249",
            Epoch = DateTime.UtcNow,
            FetchedAt = DateTime.UtcNow
        };
        _context.Satellites.Add(sat);
        _context.TleRecords.Add(tle);
        _context.SaveChanges();
        return (sat, tle);
    }

    private Pass MakePass(Guid satelliteId, Guid tleId, int orbitNumber) => new()
    {
        Id = Guid.NewGuid(),
        SatelliteId = satelliteId,
        TleId = tleId,
        OrbitNumber = orbitNumber,
        Aos = DateTime.UtcNow.AddHours(1),
        Los = DateTime.UtcNow.AddHours(1).AddMinutes(10),
        MaxElevation = 45,
        AosAzimuth = 90,
        LosAzimuth = 270,
        DurationSec = 600,
        CalculatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task Schedule_ValidMultiPassRequest_ReturnsIcsWithOneVEventPerPass()
    {
        var (sat, tle) = Seed();
        var pass1 = MakePass(sat.Id, tle.Id, 1);
        var pass2 = MakePass(sat.Id, tle.Id, 2);
        _context.Passes.AddRange(pass1, pass2);
        await _context.SaveChangesAsync();

        var request = new ScheduleCalendarRequest([pass1.Id, pass2.Id], [10, 30]);

        var result = await _controller.Schedule(request);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/calendar; charset=utf-8", fileResult.ContentType);
        Assert.Equal("sattrakk-passes.ics", fileResult.FileDownloadName);

        var ics = System.Text.Encoding.UTF8.GetString(fileResult.FileContents);
        Assert.Equal(1, CountOccurrences(ics, "BEGIN:VCALENDAR"));
        Assert.Equal(2, CountOccurrences(ics, "BEGIN:VEVENT"));
    }

    [Fact]
    public async Task Schedule_ValidRequest_MarksOutlookSyncedOnAffectedRows()
    {
        var (sat, tle) = Seed();
        var pass = MakePass(sat.Id, tle.Id, 1);
        _context.Passes.Add(pass);
        await _context.SaveChangesAsync();

        var request = new ScheduleCalendarRequest([pass.Id], [5]);
        var result = await _controller.Schedule(request);

        Assert.IsType<FileContentResult>(result);
        Assert.True(_context.Passes.Single(p => p.Id == pass.Id).OutlookSynced);
    }

    [Fact]
    public async Task Schedule_EmptyPassIds_ReturnsBadRequest()
    {
        var request = new ScheduleCalendarRequest([], [10]);

        var result = await _controller.Schedule(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Schedule_InvalidAlertMinuteValue_ReturnsBadRequest()
    {
        var (sat, tle) = Seed();
        var pass = MakePass(sat.Id, tle.Id, 1);
        _context.Passes.Add(pass);
        await _context.SaveChangesAsync();

        // 20 is not in the allowed set {5, 10, 15, 30, 60}.
        var request = new ScheduleCalendarRequest([pass.Id], [20]);

        var result = await _controller.Schedule(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Schedule_UnknownPassId_ReturnsNotFound()
    {
        var request = new ScheduleCalendarRequest([Guid.NewGuid()], [10]);

        var result = await _controller.Schedule(request);

        Assert.IsType<NotFoundObjectResult>(result);
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
