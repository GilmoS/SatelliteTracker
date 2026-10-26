using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using SatelliteTracker.Database;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.Tests.Database.Helpers;
using Xunit;

namespace SatelliteTracker.Tests.Database;

// What a retrying execution strategy (DatabaseRetryPolicy) does to the units of work that matter
// most: the pass upsert (one explicit transaction) and the notification-log claims. A transient
// fault is injected once, either before the work commits or after it committed with the
// acknowledgement lost, and the replay must leave no duplicate and no lost update.
public class ExecutionStrategyReplayTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

    private readonly AppDbContext _seedContext;
    private readonly SqliteConnection _connection;
    private readonly Satellite _sat;
    private readonly TleRecord _tle;
    private readonly TleRecord _newerTle;
    private readonly ApiKey _apiKey;

    public ExecutionStrategyReplayTests()
    {
        (_seedContext, _connection) = TestDbContextFactory.Create();

        _sat = new Satellite { Id = Guid.NewGuid(), Name = "EROS C3", NoradId = 54880, IsActive = true, CreatedAt = Now };
        _tle = MakeTle(Now.AddDays(-1));
        _newerTle = MakeTle(Now.AddHours(-1));
        _apiKey = new ApiKey
        {
            Id = Guid.NewGuid(), Email = "tester@example.com", DisplayName = "Tester", KeyHash = "hash",
            IsActive = true, CreatedAt = Now
        };

        _seedContext.Satellites.Add(_sat);
        _seedContext.TleRecords.AddRange(_tle, _newerTle);
        _seedContext.ApiKeys.Add(_apiKey);
        _seedContext.SaveChanges();
        _seedContext.ChangeTracker.Clear();
    }

    public void Dispose()
    {
        _seedContext.Dispose();
        _connection.Dispose();
    }

    private TleRecord MakeTle(DateTime fetchedAt) => new()
    {
        Id = Guid.NewGuid(), SatelliteId = _sat?.Id ?? Guid.Empty,
        Line1 = "1 54880U 22179A   26253.27827296  .00002487  00000-0  13073-3 0  9995",
        Line2 = "2 54880 139.3347 131.0916 0008568 215.9148 144.1084 15.11381575205203",
        Epoch = fetchedAt, FetchedAt = fetchedAt
    };

    private Pass MakePass(int orbitNumber, DateTime aos, Guid? tleId = null) => new()
    {
        Id = Guid.NewGuid(), SatelliteId = _sat.Id, TleId = tleId ?? _tle.Id, OrbitNumber = orbitNumber,
        Aos = aos, Los = aos.AddMinutes(9), MaxElevation = 40, AosAzimuth = 100, LosAzimuth = 250,
        DurationSec = 540, CalculatedAt = Now.AddHours(-1)
    };

    private void Seed(params object[] entities)
    {
        _seedContext.AddRange(entities);
        _seedContext.SaveChanges();
        _seedContext.ChangeTracker.Clear();
    }

    private AppDbContext Fresh() => TestDbContextFactory.CreateAdditionalContext(_connection);

    private static bool IsInsert(string sql) => sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase);
    private static bool IsUpdate(string sql) => sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase);

    // ── Production wiring ────────────────────────────────────────────────────

    [Fact]
    public void UseAppNpgsql_ConfiguresARetryingStrategy()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseAppNpgsql("Host=localhost;Database=db;Username=user");
        using var context = new AppDbContext(options.Options);

        var strategy = context.Database.CreateExecutionStrategy();

        Assert.IsType<NpgsqlRetryingExecutionStrategy>(strategy);
        Assert.True(strategy.RetriesOnFailure);
        Assert.Equal(6, DatabaseRetryPolicy.MaxRetryCount);
        Assert.Equal(TimeSpan.FromSeconds(6), DatabaseRetryPolicy.MaxRetryDelay);
    }

    // ── Pass upsert ──────────────────────────────────────────────────────────

    // Without a strategy-wrapped transaction, a retrying strategy rejects BeginTransaction outright.
    [Fact]
    public async Task Upsert_UnderRetryingStrategy_Succeeds()
    {
        using var context = RetryingTestDbContext.Create(_connection);

        var result = await new PassRepository(context).UpsertUpcomingAsync(_sat.Id, Now, [MakePass(100, Now.AddHours(3))]);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(new PassUpsertResult(Inserted: 1, Updated: 0, Deleted: 0, SkippedPast: 0), result.Value);
    }

    [Fact]
    public async Task Upsert_TransientFailureBeforeCommit_ReplaysWholeUnitFromTheDatabase()
    {
        var toUpdate = MakePass(100, Now.AddHours(3));
        var toDelete = MakePass(101, Now.AddHours(4).AddMinutes(30));
        Seed(toUpdate, toDelete);

        // Fails as the transaction commits, after SaveChanges accepted its changes into the
        // context. Replaying on that tracked state would see pass 100 as already up to date and
        // never write its update; the replay has to start from what the database holds.
        var fault = new FailOnceAtCommitInterceptor(afterCommit: false);
        using var context = RetryingTestDbContext.Create(_connection, fault);
        var newAos = Now.AddHours(3).AddMinutes(1);
        var inserted = MakePass(102, Now.AddHours(6));

        var result = await new PassRepository(context).UpsertUpcomingAsync(_sat.Id, Now,
            [MakePass(100, newAos, _newerTle.Id), inserted]);

        Assert.True(fault.Fired);
        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(new PassUpsertResult(Inserted: 1, Updated: 1, Deleted: 1, SkippedPast: 0), result.Value);

        using var db = Fresh();
        var passes = db.Passes.Where(p => p.SatelliteId == _sat.Id).OrderBy(p => p.OrbitNumber).ToList();
        Assert.Equal([100, 102], passes.Select(p => p.OrbitNumber));
        Assert.Equal(toUpdate.Id, passes[0].Id);
        Assert.Equal(newAos, passes[0].Aos);          // the update was not lost on replay
        Assert.Equal(_newerTle.Id, passes[0].TleId);
        Assert.Equal(inserted.Id, passes[1].Id);
    }

    [Fact]
    public async Task Upsert_CommitAcknowledgementLost_ReplayIsIdempotent()
    {
        var toUpdate = MakePass(100, Now.AddHours(3));
        var toDelete = MakePass(101, Now.AddHours(4).AddMinutes(30));
        var subscription = new PassSubscription
        {
            Id = Guid.NewGuid(), PassId = toUpdate.Id, ApiKeyId = _apiKey.Id, Notify = true, UpdatedAt = Now
        };
        Seed(toUpdate, toDelete, subscription);

        var fault = new FailOnceAtCommitInterceptor(afterCommit: true);
        using var context = RetryingTestDbContext.Create(_connection, fault);
        var newAos = Now.AddHours(3).AddMinutes(1);
        var inserted = MakePass(102, Now.AddHours(6));

        var result = await new PassRepository(context).UpsertUpcomingAsync(_sat.Id, Now,
            [MakePass(100, newAos, _newerTle.Id), inserted]);

        Assert.True(fault.Fired);
        Assert.True(result.IsSuccess, result.Error);
        // The replay saw the first attempt's committed work: both passes exist, nothing to delete.
        Assert.Equal(new PassUpsertResult(Inserted: 0, Updated: 2, Deleted: 0, SkippedPast: 0), result.Value);

        using var db = Fresh();
        var passes = db.Passes.Where(p => p.SatelliteId == _sat.Id).OrderBy(p => p.OrbitNumber).ToList();
        Assert.Equal([100, 102], passes.Select(p => p.OrbitNumber));
        Assert.Equal(toUpdate.Id, passes[0].Id);
        Assert.Equal(newAos, passes[0].Aos);
        Assert.Equal(inserted.Id, passes[1].Id);
        Assert.Single(db.PassSubscriptions.Where(s => s.PassId == toUpdate.Id));
    }

    // ── Notification-log claims ──────────────────────────────────────────────

    private PassNotificationLog MakeLog(Guid passId, PassNotificationStatus status, int attemptCount) => new()
    {
        Id = Guid.NewGuid(), PassId = passId, ApiKeyId = _apiKey.Id, AlertMinutes = 10,
        Status = status, AttemptCount = attemptCount, LastAttemptAt = Now
    };

    // The claim insert took effect but its acknowledgement was lost. The replayed insert hits the
    // unique index and reports "already claimed", so the job does not send: at most once, never
    // twice. The row stays Pending, like a crash mid-send.
    [Fact]
    public async Task TryInsert_AcknowledgementLost_ReplayReportsAlreadyClaimed_AndLeavesOneRow()
    {
        var pass = MakePass(100, Now.AddHours(3));
        Seed(pass);

        var fault = new FailOnceAfterCommandInterceptor(IsInsert);
        using var context = RetryingTestDbContext.Create(_connection, fault);

        var result = await new PassNotificationLogRepository(context)
            .TryInsertAsync(MakeLog(pass.Id, PassNotificationStatus.Pending, attemptCount: 1));

        Assert.True(fault.Fired);
        Assert.True(result.IsSuccess, result.Error);
        Assert.False(result.Value);

        using var db = Fresh();
        var log = Assert.Single(db.PassNotificationLogs.Where(l => l.PassId == pass.Id));
        Assert.Equal(PassNotificationStatus.Pending, log.Status);
        Assert.Equal(1, log.AttemptCount);
    }

    // Same for a retry claim: the conditional update already moved the row to Pending, so the
    // replay matches no row and the retry is not sent a second time.
    [Fact]
    public async Task TryClaimRetry_AcknowledgementLost_ReplayDoesNotClaimTwice()
    {
        var pass = MakePass(100, Now.AddHours(3));
        var failed = MakeLog(pass.Id, PassNotificationStatus.Failed, attemptCount: 1);
        Seed(pass, failed);

        var fault = new FailOnceAfterCommandInterceptor(IsUpdate);
        using var context = RetryingTestDbContext.Create(_connection, fault);

        var result = await new PassNotificationLogRepository(context)
            .TryClaimRetryAsync(failed.Id, expectedAttemptCount: 1, attemptAt: Now);

        Assert.True(fault.Fired);
        Assert.True(result.IsSuccess, result.Error);
        Assert.False(result.Value);

        using var db = Fresh();
        var log = Assert.Single(db.PassNotificationLogs.Where(l => l.Id == failed.Id));
        Assert.Equal(PassNotificationStatus.Pending, log.Status);
        Assert.Equal(2, log.AttemptCount);
    }
}
