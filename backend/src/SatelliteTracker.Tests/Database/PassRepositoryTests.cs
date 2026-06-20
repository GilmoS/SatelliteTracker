using Microsoft.Data.Sqlite;
using SatelliteTracker.Database;
using Xunit;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.Tests.Database.Helpers;

namespace SatelliteTracker.Tests.Database;

public class PassRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly PassRepository _repo;

    public PassRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _repo = new PassRepository(_context);
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
            Name = "TEST-SAT",
            NoradId = 25544,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var tle = new TleRecord
        {
            Id = Guid.NewGuid(),
            SatelliteId = sat.Id,
            Line1 = "1 25544U 98067A   23001.00000000  .00000000  00000-0  00000-0 0  9999",
            Line2 = "2 25544  51.6400 208.9163 0001382  95.6617 344.7248 15.49309522223145",
            Epoch = DateTime.UtcNow,
            FetchedAt = DateTime.UtcNow
        };
        _context.Satellites.Add(sat);
        _context.TleRecords.Add(tle);
        _context.SaveChanges();
        return (sat, tle);
    }

    private Pass MakePass(Guid satelliteId, Guid tleId, DateTime aos) => new()
    {
        Id = Guid.NewGuid(),
        SatelliteId = satelliteId,
        TleId = tleId,
        Aos = aos,
        Los = aos.AddMinutes(10),
        MaxElevation = 45,
        AosAzimuth = 90,
        LosAzimuth = 270,
        DurationSec = 600,
        CalculatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GetUpcomingAsync_ReturnsPassesInWindow()
    {
        var (sat, tle) = Seed();
        var now = DateTime.UtcNow;
        _context.Passes.Add(MakePass(sat.Id, tle.Id, now.AddHours(1)));
        _context.Passes.Add(MakePass(sat.Id, tle.Id, now.AddDays(10)));
        await _context.SaveChangesAsync();

        var result = await _repo.GetUpcomingAsync(sat.Id, now, now.AddDays(2));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
    }

    [Fact]
    public async Task GetUpcomingAsync_NoPassesInWindow_ReturnsEmpty()
    {
        var (sat, _) = Seed();

        var result = await _repo.GetUpcomingAsync(sat.Id, DateTime.UtcNow, DateTime.UtcNow.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsCompletedPasses()
    {
        var (sat, tle) = Seed();
        var past = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddDays(-2));
        var future = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddHours(2));
        _context.Passes.AddRange(past, future);
        await _context.SaveChangesAsync();

        var result = await _repo.GetHistoryAsync(sat.Id, DateTime.UtcNow.AddDays(-7));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal(past.Id, result.Value!.First().Id);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsPass()
    {
        var (sat, tle) = Seed();
        var pass = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddHours(1));
        _context.Passes.Add(pass);
        await _context.SaveChangesAsync();

        var result = await _repo.GetByIdAsync(pass.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(pass.Id, result.Value!.Id);
    }

    [Fact]
    public async Task GetByIdAsync_MissingId_ReturnsFailure()
    {
        var result = await _repo.GetByIdAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task AddAsync_PersistsPass()
    {
        var (sat, tle) = Seed();
        var pass = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddHours(1));

        var result = await _repo.AddAsync(pass);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _context.Passes.Count());
    }

    [Fact]
    public async Task AddRangeAsync_PersistsMultiplePasses()
    {
        var (sat, tle) = Seed();
        var passes = new[]
        {
            MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddHours(1)),
            MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddHours(2)),
            MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddHours(3))
        };

        var result = await _repo.AddRangeAsync(passes);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, _context.Passes.Count());
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var (sat, tle) = Seed();
        var pass = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddHours(1));
        _context.Passes.Add(pass);
        await _context.SaveChangesAsync();

        pass.NotificationSent = true;
        var result = await _repo.UpdateAsync(pass);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.NotificationSent);
    }

    [Fact]
    public async Task UpdateNotifyAsync_ExistingPass_ReturnsSuccess()
    {
        var (sat, tle) = Seed();
        var pass = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddHours(1));
        _context.Passes.Add(pass);
        await _context.SaveChangesAsync();

        var result = await _repo.UpdateNotifyAsync(pass.Id, true);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Notify);
    }

    [Fact]
    public async Task UpdateNotifyAsync_NonExistentPass_ReturnsFailure()
    {
        var result = await _repo.UpdateNotifyAsync(Guid.NewGuid(), true);

        Assert.False(result.IsSuccess);
        Assert.Equal("Pass not found", result.Error);
    }
}
