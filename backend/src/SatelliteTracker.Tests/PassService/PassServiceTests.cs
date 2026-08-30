using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.PassService.SGP4;
using SatelliteTracker.PassService.Services;
using Xunit;

namespace SatelliteTracker.Tests.PassService;

public class PassServiceTests
{
    private const int TestNoradId = 25544;
    private static readonly Guid TestSatelliteId = Guid.NewGuid();

    private static readonly Satellite TestSatellite = new()
    {
        Id = TestSatelliteId,
        Name = "ISS",
        NoradId = TestNoradId,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    // ISS TLE — 51.6° inclination, passes regularly over Israel (32°N)
    private const string IssLine1 = "1 25544U 98067A   21275.52333333  .00001234  00000-0  29279-4 0  9995";
    private const string IssLine2 = "2 25544  51.6443 213.0093 0004099  83.6831 276.4595 15.48919800303249";

    // Near-equatorial TLE — 2° inclination, never rises above horizon from 32°N
    private const string LowIncLine2 = "2 25544   2.0000 213.0093 0004099  83.6831 276.4595 15.48919800303248";

    private static readonly Guid TestTleId = Guid.NewGuid();


   

    private static TleRecord MakeTleRecord(string line2) => new()
    {
        Id = TestTleId,
        SatelliteId = TestSatelliteId,
        Line1 = IssLine1,
        Line2 = line2,
        Epoch = DateTime.UtcNow,
        FetchedAt = DateTime.UtcNow
    };

    private static (IPassService service, Mock<ISatelliteRepository> satRepo, Mock<ITleRepository> tleRepo,Mock<IPassRepository> passRepo)
        CreateService()
    {
        var satRepo = new Mock<ISatelliteRepository>();
        var tleRepo = new Mock<ITleRepository>();
        var passRepo = new Mock<IPassRepository>();
        var logger = new Mock<ILogger<SatelliteTracker.PassService.Services.PassService>>();
        var observerOptions = Options.Create(new ObserverSettings
        {
            Lat = 32.0055,
            Lng = 34.8854,
            AltMeters = 135.0,
            MinElevationDeg = 5.0
        });
        var service = new SatelliteTracker.PassService.Services.PassService(
            satRepo.Object, tleRepo.Object, passRepo.Object, logger.Object, observerOptions);
        return (service, satRepo, tleRepo, passRepo);
    }

    // ── CalculateAndSavePassesAsync ──────────────────────────────────────────

    [Fact]
    public async Task CalculateAndSavePassesAsync_SatelliteNotFound_ReturnsFailure()
    {
        var (service, satRepo, _, _) = CreateService();

        satRepo.Setup(r => r.GetByIdAsync(TestSatelliteId))
            .ReturnsAsync(Result<Satellite>.Failure("Satellite not found."));

        var result = await service.CalculateAndSavePassesAsync(TestSatelliteId);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CalculateAndSavePassesAsync_TleNotFound_ReturnsFailure()
    {
        var (service, satRepo, tleRepo, _) = CreateService();

        satRepo.Setup(r => r.GetByIdAsync(TestSatelliteId))
            .ReturnsAsync(Result<Satellite>.Success(TestSatellite));

        tleRepo.Setup(r => r.GetLatestByNoradIdAsync(TestNoradId))
            .ReturnsAsync(Result<TleRecord>.Failure("No TLE record found."));

        var result = await service.CalculateAndSavePassesAsync(TestSatelliteId);

        Assert.False(result.IsSuccess);
        Assert.Contains("TLE", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CalculateAndSavePassesAsync_ValidIsstle_ReturnsSuccess()
    {
        var (service, satRepo, tleRepo, passRepo) = CreateService();

        satRepo.Setup(r => r.GetByIdAsync(TestSatelliteId))
            .ReturnsAsync(Result<Satellite>.Success(TestSatellite));

        tleRepo.Setup(r => r.GetLatestByNoradIdAsync(TestNoradId))
            .ReturnsAsync(Result<TleRecord>.Success(MakeTleRecord(IssLine2)));

        passRepo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Pass>>()))
            .ReturnsAsync(Result<bool>.Success(true));

        var result = await service.CalculateAndSavePassesAsync(TestSatelliteId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        satRepo.Verify(r => r.GetByIdAsync(TestSatelliteId), Times.Once);
        tleRepo.Verify(r => r.GetLatestByNoradIdAsync(TestNoradId), Times.Once);
    }

    [Fact]
    public async Task CalculateAndSavePassesAsync_ComputesOrbitNumber_FromTleRevolutionAndElapsedOrbits()
    {
        var (service, satRepo, tleRepo, passRepo) = CreateService();

        satRepo.Setup(r => r.GetByIdAsync(TestSatelliteId))
            .ReturnsAsync(Result<Satellite>.Success(TestSatellite));

        tleRepo.Setup(r => r.GetLatestByNoradIdAsync(TestNoradId))
            .ReturnsAsync(Result<TleRecord>.Success(MakeTleRecord(IssLine2)));

        List<Pass>? savedPasses = null;
        passRepo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Pass>>()))
            .Callback<IEnumerable<Pass>>(p => savedPasses = p.ToList())
            .ReturnsAsync(Result<bool>.Success(true));

        var result = await service.CalculateAndSavePassesAsync(TestSatelliteId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(savedPasses);
        Assert.NotEmpty(savedPasses!);

        // Independently derive the expected orbit number from the same TLE, parsed fresh here
        // (TleParser itself is covered by TleParserTests.cs), to verify PassService actually
        // wires the parsed epoch/mean-motion/revolution-number into the calculation rather than
        // hardcoding a placeholder.
        var tle = TleParser.Parse(IssLine1, IssLine2);
        double orbitalPeriodMinutes = 1440.0 / tle.MeanMotion;

        foreach (var pass in savedPasses!)
        {
            int expectedOrbitNumber = tle.RevolutionNumber +
                (int)Math.Floor((pass.Aos - tle.Epoch).TotalMinutes / orbitalPeriodMinutes);

            Assert.Equal(expectedOrbitNumber, pass.OrbitNumber);
            Assert.NotEqual(0, pass.OrbitNumber);
        }
    }

    [Fact]
    public async Task CalculateAndSavePassesAsync_OrbitNumbers_AreNonDecreasing_AcrossPassesOrderedByAos()
    {
        var (service, satRepo, tleRepo, passRepo) = CreateService();

        satRepo.Setup(r => r.GetByIdAsync(TestSatelliteId))
            .ReturnsAsync(Result<Satellite>.Success(TestSatellite));

        tleRepo.Setup(r => r.GetLatestByNoradIdAsync(TestNoradId))
            .ReturnsAsync(Result<TleRecord>.Success(MakeTleRecord(IssLine2)));

        List<Pass>? savedPasses = null;
        passRepo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Pass>>()))
            .Callback<IEnumerable<Pass>>(p => savedPasses = p.ToList())
            .ReturnsAsync(Result<bool>.Success(true));

        await service.CalculateAndSavePassesAsync(TestSatelliteId);

        Assert.NotNull(savedPasses);
        var orderedByAos = savedPasses!.OrderBy(p => p.Aos).ToList();

        for (int i = 1; i < orderedByAos.Count; i++)
        {
            Assert.True(orderedByAos[i].OrbitNumber >= orderedByAos[i - 1].OrbitNumber,
                "Orbit number must not decrease for a pass with a later AOS.");
        }
    }

    [Fact]
    public async Task CalculateAndSavePassesAsync_LowInclinationTle_ReturnsEmptyListWithoutSaving()
    {
        var (service, satRepo, tleRepo, passRepo) = CreateService();

        satRepo.Setup(r => r.GetByIdAsync(TestSatelliteId))
            .ReturnsAsync(Result<Satellite>.Success(TestSatellite));

        // 2° inclination — satellite never gets above ~-8° elevation from Ben Gurion (32°N)
        tleRepo.Setup(r => r.GetLatestByNoradIdAsync(TestNoradId))
            .ReturnsAsync(Result<TleRecord>.Success(MakeTleRecord(LowIncLine2)));

        var result = await service.CalculateAndSavePassesAsync(TestSatelliteId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
        passRepo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Pass>>()), Times.Never);
    }

    // ── GetUpcomingPassesAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetUpcomingPassesAsync_DelegatesToRepository()
    {
        var (service, _, _, passRepo) = CreateService();
        var expected = new List<Pass> { new() { Id = Guid.NewGuid(), SatelliteId = TestSatelliteId } };

        passRepo.Setup(r => r.GetUpcomingAsync(TestSatelliteId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Result<IEnumerable<Pass>>.Success(expected));

        var result = await service.GetUpcomingPassesAsync(TestSatelliteId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
    }

    // ── GetPassHistoryAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetPassHistoryAsync_DelegatesToRepository()
    {
        var (service, _, _, passRepo) = CreateService();
        var expected = new List<Pass> { new() { Id = Guid.NewGuid(), SatelliteId = TestSatelliteId } };
        var query = new PassHistoryQuery();

        passRepo.Setup(r => r.GetHistoryAsync(TestSatelliteId, It.IsAny<DateTime>(), query))
            .ReturnsAsync(Result<PagedResult<Pass>>.Success(new PagedResult<Pass>(expected, query.Page, query.PageSize, false)));

        var result = await service.GetPassHistoryAsync(TestSatelliteId, query);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Items);
    }

    // ── GetPassByIdAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetPassByIdAsync_DelegatesToRepository()
    {
        var (service, _, _, passRepo) = CreateService();
        var passId = Guid.NewGuid();
        var expected = new Pass { Id = passId, SatelliteId = TestSatelliteId };

        passRepo.Setup(r => r.GetByIdAsync(passId))
            .ReturnsAsync(Result<Pass>.Success(expected));

        var result = await service.GetPassByIdAsync(passId);

        Assert.True(result.IsSuccess);
        Assert.Equal(passId, result.Value!.Id);
    }

    // ── GetPassTrackAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetPassTrackAsync_PassNotFound_ReturnsFailure()
    {
        var (service, _, _, passRepo) = CreateService();
        var passId = Guid.NewGuid();

        passRepo.Setup(r => r.GetByIdAsync(passId))
            .ReturnsAsync(Result<Pass>.Failure($"Pass {passId} not found."));

        var result = await service.GetPassTrackAsync(passId);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetPassTrackAsync_TleNotFound_ReturnsFailure()
    {
        var (service, _, tleRepo, passRepo) = CreateService();
        var pass = new Pass { Id = Guid.NewGuid(), SatelliteId = TestSatelliteId, TleId = TestTleId, Aos = DateTime.UtcNow, Los = DateTime.UtcNow.AddMinutes(10) };

        passRepo.Setup(r => r.GetByIdAsync(pass.Id)).ReturnsAsync(Result<Pass>.Success(pass));
        tleRepo.Setup(r => r.GetByIdAsync(TestTleId)).ReturnsAsync(Result<TleRecord>.Failure($"TLE record {TestTleId} not found."));

        var result = await service.GetPassTrackAsync(pass.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetPassTrackAsync_ValidPass_ReturnsTrackSpanningAosToLos()
    {
        var (service, _, tleRepo, passRepo) = CreateService();
        var tle = MakeTleRecord(IssLine2);
        // Anchor Aos/Los to the epoch parsed FROM the TLE line text (what SGP4 actually
        // propagates from) — not TleRecord.Epoch, which is just a DB metadata field.
        var parsedEpoch = TleParser.Parse(IssLine1, IssLine2).Epoch;
        var pass = new Pass
        {
            Id = Guid.NewGuid(),
            SatelliteId = TestSatelliteId,
            TleId = tle.Id,
            Aos = parsedEpoch.AddMinutes(1),
            Los = parsedEpoch.AddMinutes(11)
        };

        passRepo.Setup(r => r.GetByIdAsync(pass.Id)).ReturnsAsync(Result<Pass>.Success(pass));
        tleRepo.Setup(r => r.GetByIdAsync(tle.Id)).ReturnsAsync(Result<TleRecord>.Success(tle));

        var result = await service.GetPassTrackAsync(pass.Id);

        Assert.True(result.IsSuccess);
        var points = result.Value!.ToList();
        Assert.NotEmpty(points);
        Assert.Equal(pass.Aos, points.First().TimestampUtc);
        Assert.Equal(pass.Los, points.Last().TimestampUtc);
        Assert.All(points, p => Assert.InRange(p.TimestampUtc, pass.Aos, pass.Los));
    }

    [Fact]
    public async Task GetPassTrackAsync_UsesPassStoredTleId_NotSatelliteLatestTle()
    {
        var (service, _, tleRepo, passRepo) = CreateService();

        // "old" TLE is what the pass was actually calculated against; "newer" TLE is a different
        // orbit (low inclination) that has since been fetched for the satellite — GetPassTrackAsync
        // must never touch it.
        var oldTle = MakeTleRecord(IssLine2);
        var newerTle = new TleRecord { Id = Guid.NewGuid(), SatelliteId = TestSatelliteId, Line1 = IssLine1, Line2 = LowIncLine2, Epoch = DateTime.UtcNow, FetchedAt = DateTime.UtcNow };
        var parsedEpoch = TleParser.Parse(IssLine1, IssLine2).Epoch;

        var pass = new Pass
        {
            Id = Guid.NewGuid(),
            SatelliteId = TestSatelliteId,
            TleId = oldTle.Id,
            Aos = parsedEpoch.AddMinutes(1),
            Los = parsedEpoch.AddMinutes(11)
        };

        passRepo.Setup(r => r.GetByIdAsync(pass.Id)).ReturnsAsync(Result<Pass>.Success(pass));
        tleRepo.Setup(r => r.GetByIdAsync(oldTle.Id)).ReturnsAsync(Result<TleRecord>.Success(oldTle));
        tleRepo.Setup(r => r.GetLatestByNoradIdAsync(TestNoradId)).ReturnsAsync(Result<TleRecord>.Success(newerTle));

        var result = await service.GetPassTrackAsync(pass.Id);

        Assert.True(result.IsSuccess);
        var expected = GroundTrackCalculator.ComputeGroundTrack(TleParser.Parse(IssLine1, IssLine2), pass.Aos, pass.Los);
        Assert.Equal(expected.Select(p => p.Latitude), result.Value!.Select(p => p.Latitude));
        Assert.Equal(expected.Select(p => p.Longitude), result.Value!.Select(p => p.Longitude));

        tleRepo.Verify(r => r.GetByIdAsync(oldTle.Id), Times.Once);
        tleRepo.Verify(r => r.GetLatestByNoradIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Theory]
    [InlineData(-3)] // pass entirely before the TLE epoch — a historical pass
    [InlineData(3)]  // pass entirely after the TLE epoch — a future pass
    public async Task GetPassTrackAsync_WorksIdenticallyForPastAndFutureRelativePasses(int daysFromEpochToAos)
    {
        var (service, _, tleRepo, passRepo) = CreateService();
        var tle = MakeTleRecord(IssLine2);
        var parsedEpoch = TleParser.Parse(IssLine1, IssLine2).Epoch;
        var pass = new Pass
        {
            Id = Guid.NewGuid(),
            SatelliteId = TestSatelliteId,
            TleId = tle.Id,
            Aos = parsedEpoch.AddDays(daysFromEpochToAos),
            Los = parsedEpoch.AddDays(daysFromEpochToAos).AddMinutes(10)
        };

        passRepo.Setup(r => r.GetByIdAsync(pass.Id)).ReturnsAsync(Result<Pass>.Success(pass));
        tleRepo.Setup(r => r.GetByIdAsync(tle.Id)).ReturnsAsync(Result<TleRecord>.Success(tle));

        var result = await service.GetPassTrackAsync(pass.Id);

        Assert.True(result.IsSuccess);
        var points = result.Value!.ToList();
        Assert.NotEmpty(points);
        Assert.All(points, p => Assert.False(double.IsNaN(p.Latitude) || double.IsNaN(p.Longitude)));
    }
}
