using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SatelliteTracker.Database;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.PassService.SGP4;
using SatelliteTracker.Tests.Database.Helpers;
using Xunit;

namespace SatelliteTracker.Tests.Database;

// PassRepository.UpsertUpcomingAsync against real SQLite: pass identity by (SatelliteId,
// OrbitNumber) across recalculations, and the Aos >= now scope boundary.
public class PassRepositoryUpsertTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

    // Real EROS C3 TLE from the dev database, for the end-to-end recalculation test.
    private const string ErosLine1 = "1 54880U 22179A   26253.27827296  .00002487  00000-0  13073-3 0  9995";
    private const string ErosLine2 = "2 54880 139.3347 131.0916 0008568 215.9148 144.1084 15.11381575205203";

    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly PassRepository _repo;
    private readonly Satellite _sat;
    private readonly TleRecord _tle;
    private readonly TleRecord _newerTle;
    private readonly ApiKey _apiKey;

    public PassRepositoryUpsertTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _repo = new PassRepository(_context);

        _sat = new Satellite { Id = Guid.NewGuid(), Name = "EROS C3", NoradId = 54880, IsActive = true, CreatedAt = Now };
        _tle = MakeTle(Now.AddDays(-1));
        _newerTle = MakeTle(Now.AddHours(-1));
        _apiKey = new ApiKey
        {
            Id = Guid.NewGuid(), Email = "tester@example.com", DisplayName = "Tester", KeyHash = "hash",
            IsActive = true, CreatedAt = Now
        };

        _context.Satellites.Add(_sat);
        _context.TleRecords.AddRange(_tle, _newerTle);
        _context.ApiKeys.Add(_apiKey);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private TleRecord MakeTle(DateTime fetchedAt) => new()
    {
        Id = Guid.NewGuid(), SatelliteId = _sat?.Id ?? Guid.Empty, Line1 = ErosLine1, Line2 = ErosLine2,
        Epoch = fetchedAt, FetchedAt = fetchedAt
    };

    private Pass MakePass(int orbitNumber, DateTime aos, Guid? tleId = null) => new()
    {
        Id = Guid.NewGuid(),
        SatelliteId = _sat.Id,
        TleId = tleId ?? _tle.Id,
        OrbitNumber = orbitNumber,
        Aos = aos,
        Los = aos.AddMinutes(9),
        MaxElevation = 40,
        AosAzimuth = 100,
        LosAzimuth = 250,
        DurationSec = 540,
        CalculatedAt = Now.AddHours(-1)
    };

    // Seeds through a separate context, so the repository's context tracks none of these rows and
    // any cascade has to come from the database itself, as in production.
    private void SeedUntracked(params object[] entities)
    {
        using var seed = TestDbContextFactory.CreateAdditionalContext(_connection);
        seed.AddRange(entities);
        seed.SaveChanges();
    }

    // Reads through a fresh context: only what was actually committed.
    private AppDbContext Fresh() => TestDbContextFactory.CreateAdditionalContext(_connection);

    private PassSubscription MakeSubscription(Guid passId) => new()
    {
        Id = Guid.NewGuid(), PassId = passId, ApiKeyId = _apiKey.Id, Notify = true, UpdatedAt = Now
    };

    // ── Identity across recalculation ────────────────────────────────────────

    [Fact]
    public async Task Recalculation_SameOrbitNumber_KeepsSameId()
    {
        var existing = MakePass(100, Now.AddHours(3));
        SeedUntracked(existing);

        var recomputed = MakePass(100, Now.AddHours(3).AddSeconds(4), _newerTle.Id);
        var result = await _repo.UpsertUpcomingAsync(_sat.Id, Now, [recomputed]);

        Assert.True(result.IsSuccess);
        Assert.Equal(new PassUpsertResult(Inserted: 0, Updated: 1, Deleted: 0, SkippedPast: 0), result.Value);
        using var db = Fresh();
        var row = Assert.Single(db.Passes.Where(p => p.SatelliteId == _sat.Id));
        Assert.Equal(existing.Id, row.Id);
    }

    [Fact]
    public async Task Recalculation_PassSubscription_Survives()
    {
        var existing = MakePass(100, Now.AddHours(3));
        var subscription = MakeSubscription(existing.Id);
        SeedUntracked(existing, subscription);

        await _repo.UpsertUpcomingAsync(_sat.Id, Now, [MakePass(100, Now.AddHours(3).AddSeconds(4))]);

        using var db = Fresh();
        var row = Assert.Single(db.PassSubscriptions);
        Assert.Equal(subscription.Id, row.Id);
        Assert.Equal(existing.Id, row.PassId);
        Assert.True(row.Notify);
    }

    [Fact]
    public async Task Recalculation_Note_Survives()
    {
        var existing = MakePass(100, Now.AddHours(3));
        var note = new Note { Id = Guid.NewGuid(), PassId = existing.Id, Content = "clear sky", CreatedAt = Now, UpdatedAt = Now };
        SeedUntracked(existing, note);

        await _repo.UpsertUpcomingAsync(_sat.Id, Now, [MakePass(100, Now.AddHours(3).AddSeconds(4))]);

        using var db = Fresh();
        var row = Assert.Single(db.Notes);
        Assert.Equal(existing.Id, row.PassId);
        Assert.Equal("clear sky", row.Content);
    }

    [Fact]
    public async Task Recalculation_PassNotificationLog_Survives()
    {
        var existing = MakePass(100, Now.AddHours(3));
        var log = new PassNotificationLog { Id = Guid.NewGuid(), PassId = existing.Id, ApiKeyId = _apiKey.Id, AlertMinutes = 30, SentAt = Now };
        SeedUntracked(existing, log);

        await _repo.UpsertUpcomingAsync(_sat.Id, Now, [MakePass(100, Now.AddHours(3).AddSeconds(4))]);

        using var db = Fresh();
        var row = Assert.Single(db.PassNotificationLogs);
        Assert.Equal(existing.Id, row.PassId);
        Assert.Equal(30, row.AlertMinutes);
    }

    [Fact]
    public async Task Recalculation_UpdatesComputedFieldsInPlace()
    {
        var existing = MakePass(100, Now.AddHours(3));
        existing.OutlookSynced = true;
        SeedUntracked(existing);

        var recomputed = new Pass
        {
            Id = Guid.NewGuid(), // a fresh Guid from PassService — must not replace the existing Id
            SatelliteId = _sat.Id,
            TleId = _newerTle.Id,
            OrbitNumber = 100,
            Aos = Now.AddHours(3).AddSeconds(7),
            Los = Now.AddHours(3).AddMinutes(9).AddSeconds(2),
            MaxElevation = 41.5m,
            AosAzimuth = 101.25m,
            LosAzimuth = 249.75m,
            DurationSec = 535,
            OutlookSynced = false,
            CalculatedAt = Now
        };

        await _repo.UpsertUpcomingAsync(_sat.Id, Now, [recomputed]);

        using var db = Fresh();
        var row = Assert.Single(db.Passes.Where(p => p.SatelliteId == _sat.Id));
        Assert.Equal(existing.Id, row.Id);
        Assert.Equal(recomputed.Aos, row.Aos);
        Assert.Equal(recomputed.Los, row.Los);
        Assert.Equal(41.5m, row.MaxElevation);
        Assert.Equal(101.25m, row.AosAzimuth);
        Assert.Equal(249.75m, row.LosAzimuth);
        Assert.Equal(535, row.DurationSec);
        Assert.Equal(_newerTle.Id, row.TleId);
        Assert.Equal(Now, row.CalculatedAt);
        // Not a computed field: the upsert leaves it alone.
        Assert.True(row.OutlookSynced);
    }

    // ── Insert / delete ──────────────────────────────────────────────────────

    [Fact]
    public async Task NewOrbit_IsInserted()
    {
        var existing = MakePass(100, Now.AddHours(3));
        SeedUntracked(existing);

        var newPass = MakePass(101, Now.AddHours(4).AddMinutes(30));
        var result = await _repo.UpsertUpcomingAsync(_sat.Id, Now,
            [MakePass(100, Now.AddHours(3)), newPass]);

        Assert.Equal(new PassUpsertResult(Inserted: 1, Updated: 1, Deleted: 0, SkippedPast: 0), result.Value);
        using var db = Fresh();
        var inserted = Assert.Single(db.Passes.Where(p => p.OrbitNumber == 101));
        Assert.Equal(newPass.Id, inserted.Id);
        Assert.Equal(newPass.Aos, inserted.Aos);
        Assert.Equal(2, db.Passes.Count(p => p.SatelliteId == _sat.Id));
    }

    [Fact]
    public async Task UpcomingPassMissingFromComputation_IsDeleted_AndItsSubscriptionCascades()
    {
        var kept = MakePass(100, Now.AddHours(3));
        var vanished = MakePass(101, Now.AddHours(4).AddMinutes(30));
        var keptSubscription = MakeSubscription(kept.Id);
        var vanishedSubscription = MakeSubscription(vanished.Id);
        SeedUntracked(kept, vanished, keptSubscription, vanishedSubscription);

        var result = await _repo.UpsertUpcomingAsync(_sat.Id, Now, [MakePass(100, Now.AddHours(3))]);

        Assert.Equal(new PassUpsertResult(Inserted: 0, Updated: 1, Deleted: 1, SkippedPast: 0), result.Value);
        using var db = Fresh();
        Assert.Equal(kept.Id, Assert.Single(db.Passes.Where(p => p.SatelliteId == _sat.Id)).Id);
        Assert.Equal(keptSubscription.Id, Assert.Single(db.PassSubscriptions).Id);
    }

    [Fact]
    public async Task EmptyComputation_DeletesAllUpcoming_ButNotPast()
    {
        var past = MakePass(90, Now.AddHours(-5));
        var upcoming = MakePass(100, Now.AddHours(3));
        SeedUntracked(past, upcoming);

        var result = await _repo.UpsertUpcomingAsync(_sat.Id, Now, []);

        Assert.Equal(new PassUpsertResult(Inserted: 0, Updated: 0, Deleted: 1, SkippedPast: 0), result.Value);
        using var db = Fresh();
        Assert.Equal(past.Id, Assert.Single(db.Passes.Where(p => p.SatelliteId == _sat.Id)).Id);
    }

    // ── Scope boundary: Aos < now is never touched ───────────────────────────

    [Fact]
    public async Task PastAndInProgressPasses_AreNeverModifiedOrDeleted()
    {
        var past = MakePass(90, Now.AddHours(-5));
        var inProgress = MakePass(91, Now.AddMinutes(-3)); // Aos < now < Los
        var pastSubscription = MakeSubscription(past.Id);
        SeedUntracked(past, inProgress, pastSubscription);

        // The computation claims both orbits with different times (a truncated in-progress pass
        // would look like this), plus one genuinely upcoming pass.
        var result = await _repo.UpsertUpcomingAsync(_sat.Id, Now,
        [
            MakePass(90, Now.AddMinutes(1)),
            MakePass(91, Now.AddMinutes(2)),
            MakePass(92, Now.AddHours(1))
        ]);

        Assert.True(result.IsSuccess);
        Assert.Equal(new PassUpsertResult(Inserted: 1, Updated: 0, Deleted: 0, SkippedPast: 2), result.Value);

        using var db = Fresh();
        var pastRow = Assert.Single(db.Passes.Where(p => p.OrbitNumber == 90));
        Assert.Equal(past.Id, pastRow.Id);
        Assert.Equal(past.Aos, pastRow.Aos);
        Assert.Equal(past.CalculatedAt, pastRow.CalculatedAt);

        var inProgressRow = Assert.Single(db.Passes.Where(p => p.OrbitNumber == 91));
        Assert.Equal(inProgress.Id, inProgressRow.Id);
        Assert.Equal(inProgress.Aos, inProgressRow.Aos);

        Assert.Single(db.PassSubscriptions.Where(s => s.PassId == past.Id));
        Assert.Equal(3, db.Passes.Count(p => p.SatelliteId == _sat.Id));
    }

    // ── Unique index ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UniqueIndex_RejectsDuplicateSatelliteAndOrbitNumber()
    {
        SeedUntracked(MakePass(100, Now.AddHours(3)));

        using var db = Fresh();
        db.Passes.Add(MakePass(100, Now.AddHours(5)));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task UniqueIndex_AllowsSameOrbitNumberOnAnotherSatellite()
    {
        var other = new Satellite { Id = Guid.NewGuid(), Name = "RUNNER-1", NoradId = 56953, IsActive = true, CreatedAt = Now };
        var otherTle = MakeTle(Now);
        otherTle.SatelliteId = other.Id;
        var otherPass = MakePass(100, Now.AddHours(5), otherTle.Id);
        otherPass.SatelliteId = other.Id;

        SeedUntracked(MakePass(100, Now.AddHours(3)));
        SeedUntracked(other, otherTle, otherPass);

        using var db = Fresh();
        Assert.Equal(2, db.Passes.Count(p => p.OrbitNumber == 100));
    }

    // ── Atomicity ────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertFailingPartway_CommitsNothing()
    {
        var toUpdate = MakePass(100, Now.AddHours(3));
        var toDelete = MakePass(101, Now.AddHours(4).AddMinutes(30));
        SeedUntracked(toUpdate, toDelete);

        // An update, a delete, a valid insert, then two inserts sharing an orbit number: the unique
        // index fails the save after the other changes were already issued.
        var result = await _repo.UpsertUpcomingAsync(_sat.Id, Now,
        [
            MakePass(100, Now.AddHours(3).AddMinutes(1), _newerTle.Id),
            MakePass(102, Now.AddHours(6)),
            MakePass(103, Now.AddHours(8)),
            MakePass(103, Now.AddHours(9))
        ]);

        Assert.False(result.IsSuccess);

        using (var db = Fresh())
        {
            var passes = db.Passes.Where(p => p.SatelliteId == _sat.Id).OrderBy(p => p.OrbitNumber).ToList();
            Assert.Equal([100, 101], passes.Select(p => p.OrbitNumber));
            Assert.Equal(toUpdate.Aos, passes[0].Aos);
            Assert.Equal(_tle.Id, passes[0].TleId);
            Assert.Equal(toDelete.Id, passes[1].Id);
        }

        // The failed attempt left nothing behind in the repository's context either: the next
        // upsert on it succeeds and starts from the committed state.
        var retry = await _repo.UpsertUpcomingAsync(_sat.Id, Now, [MakePass(100, Now.AddHours(3))]);
        Assert.True(retry.IsSuccess);
        Assert.Equal(new PassUpsertResult(Inserted: 0, Updated: 1, Deleted: 1, SkippedPast: 0), retry.Value);
    }

    // ── End to end through PassService ───────────────────────────────────────

    [Fact]
    public async Task PassService_RecalculatingAnHourLater_KeepsIdsAndSubscriptionsOfUpcomingPasses()
    {
        var tle = TleParser.Parse(ErosLine1, ErosLine2);
        var firstRun = tle.Epoch.AddHours(1);
        var secondRun = firstRun.AddHours(1);

        await CreatePassService(firstRun).CalculateAndSavePassesAsync(_sat.Id);

        Dictionary<int, Guid> idsAfterFirstRun;
        Pass subscribed;
        using (var db = Fresh())
        {
            idsAfterFirstRun = db.Passes.Where(p => p.SatelliteId == _sat.Id).ToDictionary(p => p.OrbitNumber, p => p.Id);
            subscribed = db.Passes.Where(p => p.SatelliteId == _sat.Id && p.Aos > secondRun).OrderBy(p => p.Aos).First();
        }
        Assert.True(idsAfterFirstRun.Count > 10);
        SeedUntracked(MakeSubscription(subscribed.Id));

        _context.ChangeTracker.Clear();
        var second = await CreatePassService(secondRun).CalculateAndSavePassesAsync(_sat.Id);
        Assert.True(second.IsSuccess);

        using var after = Fresh();
        var passes = after.Passes.Where(p => p.SatelliteId == _sat.Id).ToList();
        // Every pass the first run stored is still there under its original Id — the ones that
        // went past in between untouched, the upcoming ones updated in place.
        foreach (var (orbit, id) in idsAfterFirstRun)
            Assert.Equal(id, Assert.Single(passes, p => p.OrbitNumber == orbit).Id);
        Assert.Equal(subscribed.Id, Assert.Single(after.PassSubscriptions).PassId);
    }

    private SatelliteTracker.PassService.Services.PassService CreatePassService(DateTime utcNow)
    {
        // The latest TLE the service will pick must parse to the EROS fixture (both records do).
        return new SatelliteTracker.PassService.Services.PassService(
            new SatelliteRepository(_context), new TleRepository(_context), _repo,
            NullLogger<SatelliteTracker.PassService.Services.PassService>.Instance,
            Options.Create(new ObserverSettings { Lat = 32.0055, Lng = 34.8854, AltMeters = 135.0, MinElevationDeg = 5.0 }),
            new FixedTimeProvider(utcNow));
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }
}
