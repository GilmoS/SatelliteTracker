using Microsoft.Data.Sqlite;
using SatelliteTracker.Database;
using Xunit;
using SatelliteTracker.Database.Common;
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

        var result = await _repo.GetHistoryAsync(sat.Id, DateTime.UtcNow.AddDays(-7), new PassHistoryQuery());

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Items);
        Assert.Equal(past.Id, result.Value!.Items.First().Id);
    }

    // ── GetHistoryAsync — pagination + filters ──────────────────────────────

    [Fact]
    public async Task GetHistoryAsync_NoParams_ReturnsMostRecentFirst_DefaultPageAndSize()
    {
        var (sat, tle) = Seed();
        var older = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddDays(-5));
        var newer = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddDays(-1));
        _context.Passes.AddRange(older, newer);
        await _context.SaveChangesAsync();

        var result = await _repo.GetHistoryAsync(sat.Id, DateTime.UtcNow.AddMonths(-6), new PassHistoryQuery());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Page);
        Assert.Equal(50, result.Value!.PageSize);
        Assert.False(result.Value!.HasMore);
        Assert.Equal([newer.Id, older.Id], result.Value!.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task GetHistoryAsync_ExactlyPageSizeRows_HasMoreIsFalse()
    {
        var (sat, tle) = Seed();
        for (int i = 0; i < 3; i++)
            _context.Passes.Add(MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddDays(-1 - i)));
        await _context.SaveChangesAsync();

        var query = new PassHistoryQuery { PageSize = 3 };
        var result = await _repo.GetHistoryAsync(sat.Id, DateTime.UtcNow.AddMonths(-6), query);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Items.Count);
        Assert.False(result.Value!.HasMore);
    }

    [Fact]
    public async Task GetHistoryAsync_PageSizePlusOneRowsAvailable_HasMoreIsTrue()
    {
        var (sat, tle) = Seed();
        for (int i = 0; i < 4; i++)
            _context.Passes.Add(MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddDays(-1 - i)));
        await _context.SaveChangesAsync();

        var query = new PassHistoryQuery { PageSize = 3 };
        var result = await _repo.GetHistoryAsync(sat.Id, DateTime.UtcNow.AddMonths(-6), query);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Items.Count);
        Assert.True(result.Value!.HasMore);
    }

    [Fact]
    public async Task GetHistoryAsync_PageBeyondAvailableData_ReturnsEmptyAndHasMoreFalse()
    {
        var (sat, tle) = Seed();
        _context.Passes.Add(MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddDays(-1)));
        await _context.SaveChangesAsync();

        var query = new PassHistoryQuery { Page = 5, PageSize = 10 };
        var result = await _repo.GetHistoryAsync(sat.Id, DateTime.UtcNow.AddMonths(-6), query);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Items);
        Assert.False(result.Value!.HasMore);
    }

    [Fact]
    public async Task GetHistoryAsync_OrbitNumberRange_ReturnsOnlyMatchingRows()
    {
        var (sat, tle) = Seed();
        var low = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddDays(-3));
        low.OrbitNumber = 100;
        var mid = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddDays(-2));
        mid.OrbitNumber = 150;
        var high = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddDays(-1));
        high.OrbitNumber = 200;
        _context.Passes.AddRange(low, mid, high);
        await _context.SaveChangesAsync();

        var query = new PassHistoryQuery { OrbitNumberFrom = 120, OrbitNumberTo = 180 };
        var result = await _repo.GetHistoryAsync(sat.Id, DateTime.UtcNow.AddMonths(-6), query);

        Assert.True(result.IsSuccess);
        Assert.Equal([mid.Id], result.Value!.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task GetHistoryAsync_MaxElevationRange_ReturnsOnlyMatchingRows()
    {
        var (sat, tle) = Seed();
        var low = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddDays(-3));
        low.MaxElevation = 10;
        var mid = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddDays(-2));
        mid.MaxElevation = 45;
        var high = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddDays(-1));
        high.MaxElevation = 80;
        _context.Passes.AddRange(low, mid, high);
        await _context.SaveChangesAsync();

        var query = new PassHistoryQuery { MaxElevationFrom = 30, MaxElevationTo = 60 };
        var result = await _repo.GetHistoryAsync(sat.Id, DateTime.UtcNow.AddMonths(-6), query);

        Assert.True(result.IsSuccess);
        Assert.Equal([mid.Id], result.Value!.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task GetHistoryAsync_AosRange_ReturnsOnlyMatchingRows()
    {
        var (sat, tle) = Seed();
        var earlier = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddDays(-10));
        var target = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddDays(-5));
        var later = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddDays(-1));
        _context.Passes.AddRange(earlier, target, later);
        await _context.SaveChangesAsync();

        var query = new PassHistoryQuery
        {
            AosFrom = DateTime.UtcNow.AddDays(-6),
            AosTo = DateTime.UtcNow.AddDays(-4)
        };
        var result = await _repo.GetHistoryAsync(sat.Id, DateTime.UtcNow.AddMonths(-6), query);

        Assert.True(result.IsSuccess);
        Assert.Equal([target.Id], result.Value!.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task GetHistoryAsync_LosRange_ReturnsOnlyMatchingRows()
    {
        var (sat, tle) = Seed();
        var earlier = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddDays(-10));
        var target = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddDays(-5));
        var later = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddDays(-1));
        _context.Passes.AddRange(earlier, target, later);
        await _context.SaveChangesAsync();

        var query = new PassHistoryQuery
        {
            LosFrom = target.Los.AddMinutes(-1),
            LosTo = target.Los.AddMinutes(1)
        };
        var result = await _repo.GetHistoryAsync(sat.Id, DateTime.UtcNow.AddMonths(-6), query);

        Assert.True(result.IsSuccess);
        Assert.Equal([target.Id], result.Value!.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task GetHistoryAsync_MultipleFiltersCombined_AndsThemTogether()
    {
        var (sat, tle) = Seed();
        // Matches orbitNumberFrom but not aosTo — must be excluded when both filters apply.
        var orbitMatchOnly = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddDays(-1));
        orbitMatchOnly.OrbitNumber = 200;
        // Matches aosTo but not orbitNumberFrom — must be excluded when both filters apply.
        var aosMatchOnly = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddDays(-9));
        aosMatchOnly.OrbitNumber = 50;
        // Matches both.
        var both = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddDays(-8));
        both.OrbitNumber = 200;
        _context.Passes.AddRange(orbitMatchOnly, aosMatchOnly, both);
        await _context.SaveChangesAsync();

        var query = new PassHistoryQuery { OrbitNumberFrom = 100, AosTo = DateTime.UtcNow.AddDays(-7) };
        var result = await _repo.GetHistoryAsync(sat.Id, DateTime.UtcNow.AddMonths(-6), query);

        Assert.True(result.IsSuccess);
        Assert.Equal([both.Id], result.Value!.Items.Select(p => p.Id));
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

        pass.OutlookSynced = true;
        var result = await _repo.UpdateAsync(pass);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.OutlookSynced);
    }

    [Fact]
    public async Task GetByIdsAsync_AllIdsExist_ReturnsPassesWithSatelliteIncluded()
    {
        var (sat, tle) = Seed();
        var pass1 = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddHours(1));
        var pass2 = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddHours(2));
        _context.Passes.AddRange(pass1, pass2);
        await _context.SaveChangesAsync();

        var result = await _repo.GetByIdsAsync([pass1.Id, pass2.Id]);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.All(result.Value!, p => Assert.Equal(sat.Id, p.Satellite.Id));
    }

    [Fact]
    public async Task GetByIdsAsync_SomeIdsMissing_ReturnsFailureListingMissingIds()
    {
        var (sat, tle) = Seed();
        var pass = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddHours(1));
        _context.Passes.Add(pass);
        await _context.SaveChangesAsync();

        var missingId = Guid.NewGuid();
        var result = await _repo.GetByIdsAsync([pass.Id, missingId]);

        Assert.False(result.IsSuccess);
        Assert.Contains(missingId.ToString(), result.Error);
    }

    [Fact]
    public async Task MarkOutlookSyncedAsync_SetsFlagForAllGivenPasses()
    {
        var (sat, tle) = Seed();
        var pass1 = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddHours(1));
        var pass2 = MakePass(sat.Id, tle.Id, DateTime.UtcNow.AddHours(2));
        _context.Passes.AddRange(pass1, pass2);
        await _context.SaveChangesAsync();

        var result = await _repo.MarkOutlookSyncedAsync([pass1.Id, pass2.Id]);

        Assert.True(result.IsSuccess);
        Assert.True(_context.Passes.Single(p => p.Id == pass1.Id).OutlookSynced);
        Assert.True(_context.Passes.Single(p => p.Id == pass2.Id).OutlookSynced);
    }
}
