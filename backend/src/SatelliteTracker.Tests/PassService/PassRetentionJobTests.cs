using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SatelliteTracker.Database;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.PassService.Services;
using SatelliteTracker.Tests.Database.Helpers;
using Xunit;

namespace SatelliteTracker.Tests.PassService;

public class PassRetentionJobTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly PassRepository _passRepo;
    private readonly Satellite _satellite;
    private readonly TleRecord _tle;
    private readonly ApiKey _apiKey;
    private int _nextOrbitNumber = 1;

    public PassRetentionJobTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _passRepo = new PassRepository(_context);

        _satellite = new Satellite
        {
            Id = Guid.NewGuid(), Name = "EROS C3", NoradId = 54880, IsActive = true, CreatedAt = Now
        };
        _tle = new TleRecord
        {
            Id = Guid.NewGuid(),
            SatelliteId = _satellite.Id,
            Line1 = "1 54880U 22099A   23001.00000000  .00000000  00000-0  00000-0 0  9999",
            Line2 = "2 54880  97.7000 208.9163 0001382  95.6617 344.7248 14.98120000303249",
            Epoch = Now,
            FetchedAt = Now
        };
        _apiKey = new ApiKey
        {
            Id = Guid.NewGuid(), Email = "tester@iai.co.il", DisplayName = "Tester",
            KeyHash = new string('a', 64), IsActive = true, CreatedAt = Now
        };
        _context.AddRange(_satellite, _tle, _apiKey);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static PassRetentionJob CreateJob() => new(
        new Mock<ILogger<PassRetentionJob>>().Object, new Mock<IServiceScopeFactory>().Object, TimeProvider.System);

    // A pass with a note, a subscription and a notification log, so the cascade can be checked.
    private async Task<Pass> SeedPassWithChildren(DateTime aos)
    {
        var pass = new Pass
        {
            Id = Guid.NewGuid(),
            SatelliteId = _satellite.Id,
            TleId = _tle.Id,
            OrbitNumber = _nextOrbitNumber++,
            Aos = aos,
            Los = aos.AddMinutes(10),
            MaxElevation = 45,
            AosAzimuth = 90,
            LosAzimuth = 270,
            DurationSec = 600,
            CalculatedAt = aos.AddDays(-1)
        };
        _context.Passes.Add(pass);
        _context.Notes.Add(new Note
        {
            Id = Guid.NewGuid(), PassId = pass.Id, Content = "note", CreatedAt = aos, UpdatedAt = aos
        });
        _context.PassSubscriptions.Add(new PassSubscription
        {
            Id = Guid.NewGuid(), PassId = pass.Id, ApiKeyId = _apiKey.Id, Notify = true, UpdatedAt = aos
        });
        _context.PassNotificationLogs.Add(new PassNotificationLog
        {
            Id = Guid.NewGuid(), PassId = pass.Id, ApiKeyId = _apiKey.Id, AlertMinutes = 10,
            Status = PassNotificationStatus.Sent, AttemptCount = 1, LastAttemptAt = aos, SentAt = aos
        });
        await _context.SaveChangesAsync();
        return pass;
    }

    [Fact]
    public void CutoffFor_IsSixMonthsAndOneDayBack()
    {
        Assert.Equal(new DateTime(2026, 4, 10, 12, 0, 0, DateTimeKind.Utc), PassRetentionJob.CutoffFor(Now));
    }

    [Fact]
    public async Task RunOnceAsync_DeletesPassesPastRetention_WithTheirChildren()
    {
        var expired = await SeedPassWithChildren(PassRetentionJob.CutoffFor(Now).AddHours(-1));
        var kept = await SeedPassWithChildren(PassRetentionJob.CutoffFor(Now).AddHours(1));
        _context.ChangeTracker.Clear();

        await CreateJob().RunOnceAsync(_passRepo, Now);

        Assert.Equal([kept.Id], await _context.Passes.Select(p => p.Id).ToListAsync());
        Assert.False(await _context.Notes.AnyAsync(n => n.PassId == expired.Id));
        Assert.False(await _context.PassSubscriptions.AnyAsync(s => s.PassId == expired.Id));
        Assert.False(await _context.PassNotificationLogs.AnyAsync(l => l.PassId == expired.Id));
        Assert.True(await _context.Notes.AnyAsync(n => n.PassId == kept.Id));
        Assert.True(await _context.PassSubscriptions.AnyAsync(s => s.PassId == kept.Id));
        Assert.True(await _context.PassNotificationLogs.AnyAsync(l => l.PassId == kept.Id));
    }

    [Fact]
    public async Task DeleteAosBeforeAsync_ReturnsDeletedCount()
    {
        await SeedPassWithChildren(Now.AddMonths(-8));
        await SeedPassWithChildren(Now.AddMonths(-7));
        await SeedPassWithChildren(Now.AddDays(1));

        var result = await _passRepo.DeleteAosBeforeAsync(PassRetentionJob.CutoffFor(Now));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value);
    }

    // Summer: Israel is UTC+3, so 03:00 local = 00:00 UTC.
    [Theory]
    [InlineData("2026-07-01T23:30:00Z", 0.5)]   // 02:30 local → today's 03:00
    [InlineData("2026-07-02T00:30:00Z", 23.5)]  // 03:30 local → tomorrow's 03:00
    [InlineData("2026-07-02T00:00:00Z", 24)]    // exactly 03:00 → the next day's run
    // Winter: Israel is UTC+2, so 03:00 local = 01:00 UTC.
    [InlineData("2026-01-15T00:00:00Z", 1)]     // 02:00 local
    // Spring forward on Fri 2026-03-27 at 02:00: 01:30 IST is only 30 real minutes before 03:00 IDT.
    [InlineData("2026-03-26T23:30:00Z", 0.5)]
    public void DelayUntilNextRun_TargetsThreeAmIsraelTime(string utcNow, double expectedHours)
    {
        var now = DateTime.Parse(utcNow, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

        Assert.Equal(TimeSpan.FromHours(expectedHours), PassRetentionJob.DelayUntilNextRun(now));
    }
}
