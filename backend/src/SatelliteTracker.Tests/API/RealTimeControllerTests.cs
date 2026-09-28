using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using SatelliteTracker.API.Controllers;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.PassService.SGP4;
using SatelliteTracker.PassService.Services;
using SatelliteTracker.TLEService.Client;
using Xunit;

namespace SatelliteTracker.Tests.API;

// GetOrbit's routing/caching behavior with IPassService mocked. The SGP4 window itself (one
// period back, one period plus padding ahead, latest TLE) is covered in
// PassServiceTests.GetOrbitTrackAsync_*.
public class RealTimeControllerTests
{
    private static readonly OrbitTrack SampleOrbit = new(
        NoradId: 54880,
        PeriodMinutes: 95.1,
        Points:
        [
            new GroundTrackPoint(32.0, 34.0, 530.0, new DateTime(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc)),
            new GroundTrackPoint(33.5, 34.8, 530.2, new DateTime(2026, 9, 27, 10, 0, 30, DateTimeKind.Utc))
        ]);

    private static (RealTimeController controller, Mock<IPassService> passService) BuildController(IMemoryCache? cache = null)
    {
        var passService = new Mock<IPassService>();
        var controller = new RealTimeController(
            new Mock<ISatelliteRepository>().Object,
            new Mock<IN2YOClient>().Object,
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new ObserverSettings()),
            passService.Object);
        return (controller, passService);
    }

    [Fact]
    public async Task GetOrbit_UnknownSatellite_ReturnsNotFound()
    {
        var (controller, passService) = BuildController();
        passService
            .Setup(s => s.GetOrbitTrackAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(Result<OrbitTrack>.Failure("Satellite not found."));

        var result = await controller.GetOrbit(Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetOrbit_ValidSatellite_ReturnsOkWithPointsInUnixSeconds()
    {
        var satelliteId = Guid.NewGuid();
        var (controller, passService) = BuildController();
        passService
            .Setup(s => s.GetOrbitTrackAsync(satelliteId, It.IsAny<DateTime>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(Result<OrbitTrack>.Success(SampleOrbit));

        var result = await controller.GetOrbit(satelliteId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<OrbitTrackDto>(ok.Value);
        Assert.Equal(satelliteId, dto.SatelliteId);
        Assert.Equal(54880, dto.NoradId);
        Assert.Equal(95.1, dto.PeriodMinutes);
        Assert.Equal(2, dto.Points.Count);
        Assert.Equal(new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), dto.Points[0].Timestamp);
    }

    [Fact]
    public async Task GetOrbit_PadsTheWindowByTheCacheLifetime()
    {
        var (controller, passService) = BuildController();
        passService
            .Setup(s => s.GetOrbitTrackAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(Result<OrbitTrack>.Success(SampleOrbit));

        await controller.GetOrbit(Guid.NewGuid());

        passService.Verify(
            s => s.GetOrbitTrackAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), TimeSpan.FromMinutes(5)), Times.Once);
    }

    [Fact]
    public async Task GetOrbit_CalledTwiceForSameSatellite_OnlyComputesOnce()
    {
        var satelliteId = Guid.NewGuid();
        var (controller, passService) = BuildController(new MemoryCache(new MemoryCacheOptions()));
        passService
            .Setup(s => s.GetOrbitTrackAsync(satelliteId, It.IsAny<DateTime>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(Result<OrbitTrack>.Success(SampleOrbit));

        await controller.GetOrbit(satelliteId);
        await controller.GetOrbit(satelliteId);

        passService.Verify(
            s => s.GetOrbitTrackAsync(satelliteId, It.IsAny<DateTime>(), It.IsAny<TimeSpan>()), Times.Once);
    }
}
