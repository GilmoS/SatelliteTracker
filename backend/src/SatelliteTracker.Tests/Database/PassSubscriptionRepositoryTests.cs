using Microsoft.Data.Sqlite;
using SatelliteTracker.Database;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.Tests.Database.Helpers;
using Xunit;

namespace SatelliteTracker.Tests.Database;

// PassSubscription is a sparse opt-out table: these tests exercise the LEFT JOIN + COALESCE
// semantics of GetEffectiveNotifyStatusAsync, which is the one thing a generic GetAsync couldn't
// express safely (a missing row is not an error — it's an implicit "notify = true").
public class PassSubscriptionRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly PassSubscriptionRepository _repo;

    public PassSubscriptionRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _repo = new PassSubscriptionRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private (Satellite sat, TleRecord tle, ApiKey apiKey) Seed()
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
        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            Email = "tester@iai.co.il",
            DisplayName = "Tester",
            KeyHash = new string('a', 64),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _context.Satellites.Add(sat);
        _context.TleRecords.Add(tle);
        _context.ApiKeys.Add(apiKey);
        _context.SaveChanges();
        return (sat, tle, apiKey);
    }

    private Pass MakePass(Guid satelliteId, Guid tleId) => new()
    {
        Id = Guid.NewGuid(),
        SatelliteId = satelliteId,
        TleId = tleId,
        Aos = DateTime.UtcNow.AddHours(1),
        Los = DateTime.UtcNow.AddHours(1).AddMinutes(10),
        MaxElevation = 45,
        AosAzimuth = 90,
        LosAzimuth = 270,
        DurationSec = 600,
        CalculatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GetEffectiveNotifyStatusAsync_NoSubscriptionRow_DefaultsToTrue()
    {
        var (sat, tle, apiKey) = Seed();
        var pass = MakePass(sat.Id, tle.Id);
        _context.Passes.Add(pass);
        await _context.SaveChangesAsync();

        var result = await _repo.GetEffectiveNotifyStatusAsync(pass.Id, apiKey.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    [Fact]
    public async Task GetEffectiveNotifyStatusAsync_ExplicitOptOutRow_ReturnsFalse()
    {
        var (sat, tle, apiKey) = Seed();
        var pass = MakePass(sat.Id, tle.Id);
        _context.Passes.Add(pass);
        await _context.SaveChangesAsync();

        await _repo.SetNotifyAsync(pass.Id, apiKey.Id, notify: false);

        var result = await _repo.GetEffectiveNotifyStatusAsync(pass.Id, apiKey.Id);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public async Task SetNotifyAsync_CalledTwice_UpsertsSingleRow()
    {
        var (sat, tle, apiKey) = Seed();
        var pass = MakePass(sat.Id, tle.Id);
        _context.Passes.Add(pass);
        await _context.SaveChangesAsync();

        await _repo.SetNotifyAsync(pass.Id, apiKey.Id, notify: false);
        await _repo.SetNotifyAsync(pass.Id, apiKey.Id, notify: true);

        Assert.Single(_context.PassSubscriptions);
        var result = await _repo.GetEffectiveNotifyStatusAsync(pass.Id, apiKey.Id);
        Assert.True(result.Value);
    }

    [Fact]
    public async Task DeleteOverrideAsync_ExistingRow_RemovesItAndRestoresDefaultTrue()
    {
        var (sat, tle, apiKey) = Seed();
        var pass = MakePass(sat.Id, tle.Id);
        _context.Passes.Add(pass);
        await _context.SaveChangesAsync();

        await _repo.SetNotifyAsync(pass.Id, apiKey.Id, notify: false);

        var result = await _repo.DeleteOverrideAsync(pass.Id, apiKey.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(_context.PassSubscriptions);
        var effective = await _repo.GetEffectiveNotifyStatusAsync(pass.Id, apiKey.Id);
        Assert.True(effective.Value);
    }

    [Fact]
    public async Task DeleteOverrideAsync_NoExistingRow_IsNoOpSuccess()
    {
        var (sat, tle, apiKey) = Seed();
        var pass = MakePass(sat.Id, tle.Id);
        _context.Passes.Add(pass);
        await _context.SaveChangesAsync();

        var result = await _repo.DeleteOverrideAsync(pass.Id, apiKey.Id);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DeleteOverrideAsync_OnlyRemovesTheGivenTestersRow()
    {
        var (sat, tle, apiKey1) = Seed();
        var apiKey2 = new ApiKey
        {
            Id = Guid.NewGuid(),
            Email = "tester2@iai.co.il",
            DisplayName = "Tester Two",
            KeyHash = new string('b', 64),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _context.ApiKeys.Add(apiKey2);
        var pass = MakePass(sat.Id, tle.Id);
        _context.Passes.Add(pass);
        await _context.SaveChangesAsync();

        await _repo.SetNotifyAsync(pass.Id, apiKey1.Id, notify: false);
        await _repo.SetNotifyAsync(pass.Id, apiKey2.Id, notify: false);

        await _repo.DeleteOverrideAsync(pass.Id, apiKey1.Id);

        var tester1Effective = await _repo.GetEffectiveNotifyStatusAsync(pass.Id, apiKey1.Id);
        var tester2Effective = await _repo.GetEffectiveNotifyStatusAsync(pass.Id, apiKey2.Id);
        Assert.True(tester1Effective.Value);
        Assert.False(tester2Effective.Value);
    }

    [Fact]
    public async Task DeleteByPassIdAsync_RemovesOnlyThatPassSubscriptions()
    {
        var (sat, tle, apiKey) = Seed();
        var pass1 = MakePass(sat.Id, tle.Id);
        var pass2 = MakePass(sat.Id, tle.Id);
        _context.Passes.AddRange(pass1, pass2);
        await _context.SaveChangesAsync();

        await _repo.SetNotifyAsync(pass1.Id, apiKey.Id, notify: false);
        await _repo.SetNotifyAsync(pass2.Id, apiKey.Id, notify: false);

        var result = await _repo.DeleteByPassIdAsync(pass1.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(_context.PassSubscriptions.Where(s => s.PassId == pass1.Id));
        Assert.Single(_context.PassSubscriptions.Where(s => s.PassId == pass2.Id));
    }

    [Fact]
    public async Task PassDeleted_CascadesSubscriptionDelete()
    {
        var (sat, tle, apiKey) = Seed();
        var pass = MakePass(sat.Id, tle.Id);
        _context.Passes.Add(pass);
        await _context.SaveChangesAsync();

        await _repo.SetNotifyAsync(pass.Id, apiKey.Id, notify: false);

        _context.Passes.Remove(pass);
        await _context.SaveChangesAsync();

        Assert.Empty(_context.PassSubscriptions);
    }
}
