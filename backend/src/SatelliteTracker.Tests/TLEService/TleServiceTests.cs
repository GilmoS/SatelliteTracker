using Moq;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.TLEService.Client;
using SatelliteTracker.TLEService.Client.DTOs;
using SatelliteTracker.TLEService.Services;
using Xunit;

namespace SatelliteTracker.Tests.TLEService;

public class TleServiceTests
{
    private const int TestNoradId = 25544;

    private static readonly Satellite TestSatellite = new()
    {
        Id = Guid.NewGuid(),
        Name = "ISS",
        NoradId = TestNoradId,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    private const string ValidTleLine1 = "1 25544U 98067A   23001.00000000  .00000000  00000-0  00000-0 0  9999";
    private const string ValidTleLine2 = "2 25544  51.6400 208.9163 0001382  95.6617 344.7248 15.49309522223145";
    private static readonly string ValidTleString = $"{ValidTleLine1}\r\n{ValidTleLine2}";

    private static (ITleService service, Mock<IN2YOClient> n2yo, Mock<ITleRepository> tleRepo, Mock<ISatelliteRepository> satRepo)
        CreateService()
    {
        var n2yo = new Mock<IN2YOClient>();
        var tleRepo = new Mock<ITleRepository>();
        var satRepo = new Mock<ISatelliteRepository>();
        var service = new TleService(n2yo.Object, tleRepo.Object, satRepo.Object);
        return (service, n2yo, tleRepo, satRepo);
    }

    // ── FetchAndSaveAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task FetchAndSaveAsync_Success_ReturnsSavedRecord()
    {
        var (service, n2yo, tleRepo, satRepo) = CreateService();

        satRepo.Setup(r => r.GetByNoradIdAsync(TestNoradId))
            .ReturnsAsync(Result<Satellite>.Success(TestSatellite));

        n2yo.Setup(c => c.GetTleAsync(TestNoradId, default))
            .ReturnsAsync(Result<TleResponse>.Success(new TleResponse
            {
                Info = new TleInfo { SatId = TestNoradId, SatName = "ISS" },
                Tle = ValidTleString
            }));

        tleRepo.Setup(r => r.AddAsync(It.IsAny<TleRecord>()))
            .ReturnsAsync((TleRecord t) => Result<TleRecord>.Success(t));

        var result = await service.FetchAndSaveAsync(TestNoradId);

        Assert.True(result.IsSuccess);
        Assert.Equal(TestSatellite.Id, result.Value!.SatelliteId);
        Assert.Equal(ValidTleLine1, result.Value.Line1);
        Assert.Equal(ValidTleLine2, result.Value.Line2);
    }

    [Fact]
    public async Task FetchAndSaveAsync_SatelliteNotFound_ReturnsFailure()
    {
        var (service, _, _, satRepo) = CreateService();

        satRepo.Setup(r => r.GetByNoradIdAsync(TestNoradId))
            .ReturnsAsync(Result<Satellite>.Failure("Satellite not found."));

        var result = await service.FetchAndSaveAsync(TestNoradId);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FetchAndSaveAsync_N2YOFailure_ReturnsFailure()
    {
        var (service, n2yo, _, satRepo) = CreateService();

        satRepo.Setup(r => r.GetByNoradIdAsync(TestNoradId))
            .ReturnsAsync(Result<Satellite>.Success(TestSatellite));

        n2yo.Setup(c => c.GetTleAsync(TestNoradId, default))
            .ReturnsAsync(Result<TleResponse>.Failure("N2YO API returned 503: Service Unavailable"));

        var result = await service.FetchAndSaveAsync(TestNoradId);

        Assert.False(result.IsSuccess);
        Assert.Contains("503", result.Error);
    }

    [Fact]
    public async Task FetchAndSaveAsync_InvalidTleString_ReturnsFailure()
    {
        var (service, n2yo, _, satRepo) = CreateService();

        satRepo.Setup(r => r.GetByNoradIdAsync(TestNoradId))
            .ReturnsAsync(Result<Satellite>.Success(TestSatellite));

        n2yo.Setup(c => c.GetTleAsync(TestNoradId, default))
            .ReturnsAsync(Result<TleResponse>.Success(new TleResponse
            {
                Tle = "only one line here"
            }));

        var result = await service.FetchAndSaveAsync(TestNoradId);

        Assert.False(result.IsSuccess);
        Assert.Contains("2 lines", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FetchAndSaveAsync_TleWithNewlineSeparator_ParsesCorrectly()
    {
        var (service, n2yo, tleRepo, satRepo) = CreateService();

        satRepo.Setup(r => r.GetByNoradIdAsync(TestNoradId))
            .ReturnsAsync(Result<Satellite>.Success(TestSatellite));

        // Unix-style \n separator instead of \r\n
        n2yo.Setup(c => c.GetTleAsync(TestNoradId, default))
            .ReturnsAsync(Result<TleResponse>.Success(new TleResponse
            {
                Tle = $"{ValidTleLine1}\n{ValidTleLine2}"
            }));

        tleRepo.Setup(r => r.AddAsync(It.IsAny<TleRecord>()))
            .ReturnsAsync((TleRecord t) => Result<TleRecord>.Success(t));

        var result = await service.FetchAndSaveAsync(TestNoradId);

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidTleLine1, result.Value!.Line1);
    }

    // ── GetLatestAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetLatestAsync_DelegatesToRepository()
    {
        var (service, _, tleRepo, _) = CreateService();
        var expected = new TleRecord { Id = Guid.NewGuid(), SatelliteId = TestSatellite.Id };

        tleRepo.Setup(r => r.GetLatestByNoradIdAsync(TestNoradId))
            .ReturnsAsync(Result<TleRecord>.Success(expected));

        var result = await service.GetLatestAsync(TestNoradId);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected.Id, result.Value!.Id);
    }

    // ── IsTleStaleAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task IsTleStaleAsync_FetchedOver2HoursAgo_ReturnsTrue()
    {
        var (service, _, tleRepo, _) = CreateService();

        tleRepo.Setup(r => r.GetLatestByNoradIdAsync(TestNoradId))
            .ReturnsAsync(Result<TleRecord>.Success(new TleRecord
            {
                FetchedAt = DateTime.UtcNow.AddHours(-3)
            }));

        var result = await service.IsTleStaleAsync(TestNoradId);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    [Fact]
    public async Task IsTleStaleAsync_FetchedRecently_ReturnsFalse()
    {
        var (service, _, tleRepo, _) = CreateService();

        tleRepo.Setup(r => r.GetLatestByNoradIdAsync(TestNoradId))
            .ReturnsAsync(Result<TleRecord>.Success(new TleRecord
            {
                FetchedAt = DateTime.UtcNow.AddMinutes(-30)
            }));

        var result = await service.IsTleStaleAsync(TestNoradId);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public async Task IsTleStaleAsync_NoTleRecord_ReturnsTrue()
    {
        var (service, _, tleRepo, _) = CreateService();

        tleRepo.Setup(r => r.GetLatestByNoradIdAsync(TestNoradId))
            .ReturnsAsync(Result<TleRecord>.Failure("No TLE record found."));

        var result = await service.IsTleStaleAsync(TestNoradId);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value); // absent ⇒ stale
    }
}
