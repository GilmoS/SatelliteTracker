using Microsoft.Extensions.Logging;
using Moq;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
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

    private static (IPassService service,
                    Mock<ISatelliteRepository> satRepo,
                    Mock<ITleRepository> tleRepo,
                    Mock<IPassRepository> passRepo)
        CreateService()
    {
        var satRepo = new Mock<ISatelliteRepository>();
        var tleRepo = new Mock<ITleRepository>();
        var passRepo = new Mock<IPassRepository>();
        var logger = new Mock<ILogger<SatelliteTracker.PassService.Services.PassService>>();
        var service = new SatelliteTracker.PassService.Services.PassService(
            satRepo.Object, tleRepo.Object, passRepo.Object, logger.Object);
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

        passRepo.Setup(r => r.GetHistoryAsync(TestSatelliteId, It.IsAny<DateTime>()))
            .ReturnsAsync(Result<IEnumerable<Pass>>.Success(expected));

        var result = await service.GetPassHistoryAsync(TestSatelliteId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
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
}
