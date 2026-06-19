using Microsoft.Data.Sqlite;
using SatelliteTracker.Database;
using Xunit;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.Tests.Database.Helpers;

namespace SatelliteTracker.Tests.Database;

public class TleRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly TleRepository _repo;

    public TleRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _repo = new TleRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private Satellite AddSatellite(int noradId = 25544)
    {
        var sat = new Satellite
        {
            Id = Guid.NewGuid(),
            Name = $"SAT-{noradId}",
            NoradId = noradId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Satellites.Add(sat);
        _context.SaveChanges();
        return sat;
    }

    private TleRecord MakeTle(Guid satelliteId, DateTime? fetchedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        SatelliteId = satelliteId,
        Line1 = "1 25544U 98067A   23001.00000000  .00000000  00000-0  00000-0 0  9999",
        Line2 = "2 25544  51.6400 208.9163 0001382  95.6617 344.7248 15.49309522223145",
        Epoch = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        FetchedAt = fetchedAt ?? DateTime.UtcNow
    };

    [Fact]
    public async Task GetLatestByNoradIdAsync_ReturnsNewest()
    {
        var sat = AddSatellite(25544);
        var older = MakeTle(sat.Id, DateTime.UtcNow.AddHours(-5));
        var newer = MakeTle(sat.Id, DateTime.UtcNow.AddHours(-1));
        _context.TleRecords.AddRange(older, newer);
        await _context.SaveChangesAsync();

        var result = await _repo.GetLatestByNoradIdAsync(25544);

        Assert.True(result.IsSuccess);
        Assert.Equal(newer.Id, result.Value!.Id);
    }

    [Fact]
    public async Task GetLatestByNoradIdAsync_NoRecord_ReturnsFailure()
    {
        var result = await _repo.GetLatestByNoradIdAsync(99999);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsRecordsAfterFrom()
    {
        var sat = AddSatellite(25544);
        var old = MakeTle(sat.Id, DateTime.UtcNow.AddDays(-10));
        var recent = MakeTle(sat.Id, DateTime.UtcNow.AddDays(-1));
        _context.TleRecords.AddRange(old, recent);
        await _context.SaveChangesAsync();

        var result = await _repo.GetHistoryAsync(25544, DateTime.UtcNow.AddDays(-5));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
    }

    [Fact]
    public async Task GetHistoryAsync_EmptyResult_ReturnsSuccessWithEmpty()
    {
        AddSatellite(25544);

        var result = await _repo.GetHistoryAsync(25544, DateTime.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task AddAsync_PersistsTleRecord()
    {
        var sat = AddSatellite(25544);
        var tle = MakeTle(sat.Id);

        var result = await _repo.AddAsync(tle);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _context.TleRecords.Count());
    }

    [Fact]
    public async Task DeleteOlderThanAsync_RemovesOldRecordsWithNoPasses()
    {
        var sat = AddSatellite(25544);
        var old = MakeTle(sat.Id, DateTime.UtcNow.AddMonths(-7));
        var recent = MakeTle(sat.Id, DateTime.UtcNow.AddDays(-1));
        _context.TleRecords.AddRange(old, recent);
        await _context.SaveChangesAsync();

        var result = await _repo.DeleteOlderThanAsync(DateTime.UtcNow.AddMonths(-6));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _context.TleRecords.Count());
    }

    [Fact]
    public async Task DeleteOlderThanAsync_PreservesRecordsWithPasses()
    {
        var sat = AddSatellite(25544);
        var oldTle = MakeTle(sat.Id, DateTime.UtcNow.AddMonths(-7));
        _context.TleRecords.Add(oldTle);
        await _context.SaveChangesAsync();

        var pass = new Pass
        {
            Id = Guid.NewGuid(),
            SatelliteId = sat.Id,
            TleId = oldTle.Id,
            Aos = DateTime.UtcNow.AddHours(1),
            Los = DateTime.UtcNow.AddHours(2),
            MaxElevation = 45,
            AosAzimuth = 90,
            LosAzimuth = 270,
            DurationSec = 360,
            CalculatedAt = DateTime.UtcNow
        };
        _context.Passes.Add(pass);
        await _context.SaveChangesAsync();

        await _repo.DeleteOlderThanAsync(DateTime.UtcNow.AddMonths(-6));

        Assert.Equal(1, _context.TleRecords.Count());
    }
}
