using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using SatelliteTracker.Database;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.Tests.Database.Helpers;
using Xunit;

namespace SatelliteTracker.Tests.Database;

// PassNotificationLog is an append-only ledger: TryInsertAsync must tolerate the unique
// (PassId, ApiKeyId, AlertMinutes) constraint being violated by a concurrent job tick instead of
// letting the exception propagate.
public class PassNotificationLogRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly PassNotificationLogRepository _repo;

    public PassNotificationLogRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _repo = new PassNotificationLogRepository(_context);
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

    // Unique per pass: (SatelliteId, OrbitNumber) is a unique index.
    private int _nextOrbitNumber = 1;

    private Pass MakePass(Guid satelliteId, Guid tleId) => new()
    {
        Id = Guid.NewGuid(),
        SatelliteId = satelliteId,
        TleId = tleId,
        OrbitNumber = _nextOrbitNumber++,
        Aos = DateTime.UtcNow.AddHours(1),
        Los = DateTime.UtcNow.AddHours(1).AddMinutes(10),
        MaxElevation = 45,
        AosAzimuth = 90,
        LosAzimuth = 270,
        DurationSec = 600,
        CalculatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task TryInsertAsync_NewKey_Succeeds()
    {
        var (sat, tle, apiKey) = Seed();
        var pass = MakePass(sat.Id, tle.Id);
        _context.Passes.Add(pass);
        await _context.SaveChangesAsync();

        var result = await _repo.TryInsertAsync(new PassNotificationLog
        {
            Id = Guid.NewGuid(),
            PassId = pass.Id,
            ApiKeyId = apiKey.Id,
            AlertMinutes = 10,
            SentAt = DateTimeOffset.UtcNow
        });

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    [Fact]
    public async Task TryInsertAsync_DuplicateKey_ReturnsFalseInsteadOfThrowing()
    {
        var (sat, tle, apiKey) = Seed();
        var pass = MakePass(sat.Id, tle.Id);
        _context.Passes.Add(pass);
        await _context.SaveChangesAsync();

        var first = await _repo.TryInsertAsync(new PassNotificationLog
        {
            Id = Guid.NewGuid(),
            PassId = pass.Id,
            ApiKeyId = apiKey.Id,
            AlertMinutes = 10,
            SentAt = DateTimeOffset.UtcNow
        });
        Assert.True(first.IsSuccess);
        Assert.True(first.Value);

        var second = await _repo.TryInsertAsync(new PassNotificationLog
        {
            Id = Guid.NewGuid(),
            PassId = pass.Id,
            ApiKeyId = apiKey.Id,
            AlertMinutes = 10,
            SentAt = DateTimeOffset.UtcNow
        });

        Assert.True(second.IsSuccess);
        Assert.False(second.Value);
        Assert.Single(_context.PassNotificationLogs);
    }

    [Fact]
    public async Task TryInsertAsync_DifferentAlertMinutes_BothSucceed()
    {
        var (sat, tle, apiKey) = Seed();
        var pass = MakePass(sat.Id, tle.Id);
        _context.Passes.Add(pass);
        await _context.SaveChangesAsync();

        var first = await _repo.TryInsertAsync(new PassNotificationLog
        {
            Id = Guid.NewGuid(), PassId = pass.Id, ApiKeyId = apiKey.Id, AlertMinutes = 5, SentAt = DateTimeOffset.UtcNow
        });
        var second = await _repo.TryInsertAsync(new PassNotificationLog
        {
            Id = Guid.NewGuid(), PassId = pass.Id, ApiKeyId = apiKey.Id, AlertMinutes = 10, SentAt = DateTimeOffset.UtcNow
        });

        Assert.True(first.Value);
        Assert.True(second.Value);
        Assert.Equal(2, _context.PassNotificationLogs.Count());
    }

    [Fact]
    public async Task DeleteByPassIdAsync_RemovesOnlyThatPassLogs()
    {
        var (sat, tle, apiKey) = Seed();
        var pass1 = MakePass(sat.Id, tle.Id);
        var pass2 = MakePass(sat.Id, tle.Id);
        _context.Passes.AddRange(pass1, pass2);
        await _context.SaveChangesAsync();

        await _repo.TryInsertAsync(new PassNotificationLog
        {
            Id = Guid.NewGuid(), PassId = pass1.Id, ApiKeyId = apiKey.Id, AlertMinutes = 5, SentAt = DateTimeOffset.UtcNow
        });
        await _repo.TryInsertAsync(new PassNotificationLog
        {
            Id = Guid.NewGuid(), PassId = pass2.Id, ApiKeyId = apiKey.Id, AlertMinutes = 5, SentAt = DateTimeOffset.UtcNow
        });

        var result = await _repo.DeleteByPassIdAsync(pass1.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(_context.PassNotificationLogs.Where(l => l.PassId == pass1.Id));
        Assert.Single(_context.PassNotificationLogs.Where(l => l.PassId == pass2.Id));
    }

    private async Task<PassNotificationLog> SeedLog(PassNotificationStatus status, int attemptCount)
    {
        var (sat, tle, apiKey) = Seed();
        var pass = MakePass(sat.Id, tle.Id);
        _context.Passes.Add(pass);
        var log = new PassNotificationLog
        {
            Id = Guid.NewGuid(),
            PassId = pass.Id,
            ApiKeyId = apiKey.Id,
            AlertMinutes = 10,
            Status = status,
            AttemptCount = attemptCount,
            LastAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            LastError = status == PassNotificationStatus.Failed ? "UNAVAILABLE" : null
        };
        _context.PassNotificationLogs.Add(log);
        await _context.SaveChangesAsync();
        return log;
    }

    // ExecuteUpdate bypasses the change tracker, so read the row back untracked.
    private Task<PassNotificationLog> Reload(Guid id) =>
        _context.PassNotificationLogs.AsNoTracking().SingleAsync(l => l.Id == id);

    [Fact]
    public async Task TryClaimRetryAsync_FailedRow_MovesToPendingAndCountsAttempt()
    {
        var log = await SeedLog(PassNotificationStatus.Failed, attemptCount: 1);
        var at = DateTimeOffset.UtcNow;

        var result = await _repo.TryClaimRetryAsync(log.Id, expectedAttemptCount: 1, at);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        var row = await Reload(log.Id);
        Assert.Equal(PassNotificationStatus.Pending, row.Status);
        Assert.Equal(2, row.AttemptCount);
    }

    [Fact]
    public async Task TryClaimRetryAsync_SecondClaimWithSameAttemptCount_Loses()
    {
        var log = await SeedLog(PassNotificationStatus.Failed, attemptCount: 1);

        var first = await _repo.TryClaimRetryAsync(log.Id, 1, DateTimeOffset.UtcNow);
        var second = await _repo.TryClaimRetryAsync(log.Id, 1, DateTimeOffset.UtcNow);

        Assert.True(first.Value);
        Assert.False(second.Value);
        Assert.Equal(2, (await Reload(log.Id)).AttemptCount);
    }

    [Theory]
    [InlineData(PassNotificationStatus.Sent)]
    [InlineData(PassNotificationStatus.Pending)]
    [InlineData(PassNotificationStatus.Abandoned)]
    public async Task TryClaimRetryAsync_RowNotFailed_IsNotClaimed(PassNotificationStatus status)
    {
        var log = await SeedLog(status, attemptCount: 1);

        var result = await _repo.TryClaimRetryAsync(log.Id, 1, DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
        Assert.Equal(status, (await Reload(log.Id)).Status);
    }

    [Fact]
    public async Task RecordOutcomeAsync_Sent_SetsSentAtAndClearsError()
    {
        var log = await SeedLog(PassNotificationStatus.Failed, attemptCount: 2);

        var result = await _repo.RecordOutcomeAsync(log.Id, PassNotificationStatus.Sent, "ignored", DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        var row = await Reload(log.Id);
        Assert.Equal(PassNotificationStatus.Sent, row.Status);
        Assert.NotNull(row.SentAt);
        Assert.Null(row.LastError);
    }

    [Fact]
    public async Task RecordOutcomeAsync_Failed_StoresErrorWithoutSentAt()
    {
        var log = await SeedLog(PassNotificationStatus.Pending, attemptCount: 1);

        await _repo.RecordOutcomeAsync(log.Id, PassNotificationStatus.Failed, new string('x', 1500), DateTimeOffset.UtcNow);

        var row = await Reload(log.Id);
        Assert.Equal(PassNotificationStatus.Failed, row.Status);
        Assert.Null(row.SentAt);
        Assert.Equal(1000, row.LastError!.Length);
    }
}
