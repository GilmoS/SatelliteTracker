using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using SatelliteTracker.API.Authentication;
using SatelliteTracker.API.Controllers;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.Database;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.PassService.SGP4;
using SatelliteTracker.PassService.Services;
using SatelliteTracker.Tests.Database.Helpers;
using Xunit;

namespace SatelliteTracker.Tests.API;

// Exercises PatchNotify's logic directly against a real PassSubscriptionRepository (SQLite
// in-memory) with IPassService mocked — [Authorize] itself isn't exercised here since filters
// don't run when a controller action is invoked directly (see RequireAdminKeyAttributeTests);
// that side is covered by the WebApplicationFactory-based AuthenticationTests instead.
public class PassesControllerTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly PassSubscriptionRepository _subscriptionRepo;

    public PassesControllerTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _subscriptionRepo = new PassSubscriptionRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static PassesController BuildController(IPassSubscriptionRepository subscriptionRepo, Pass? pass, Guid apiKeyId)
        => BuildControllerWithMock(subscriptionRepo, pass, apiKeyId).controller;

    private static (PassesController controller, Mock<IPassService> passServiceMock) BuildControllerWithMock(
        IPassSubscriptionRepository subscriptionRepo, Pass? pass, Guid apiKeyId, IMemoryCache? cache = null)
    {
        var passServiceMock = new Mock<IPassService>();
        passServiceMock
            .Setup(s => s.GetPassByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(pass is null
                ? Result<Pass>.Failure("Pass not found.")
                : Result<Pass>.Success(pass));

        var controller = new PassesController(passServiceMock.Object, subscriptionRepo, cache ?? new MemoryCache(new MemoryCacheOptions()));

        var identity = new ClaimsIdentity(
            [new Claim("api_key_id", apiKeyId.ToString())], authenticationType: "ApiKey");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        return (controller, passServiceMock);
    }

    private (Satellite sat, TleRecord tle, ApiKey apiKey1, ApiKey apiKey2) Seed()
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
        var apiKey1 = new ApiKey
        {
            Id = Guid.NewGuid(),
            Email = "tester1@iai.co.il",
            DisplayName = "Tester One",
            KeyHash = new string('a', 64),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var apiKey2 = new ApiKey
        {
            Id = Guid.NewGuid(),
            Email = "tester2@iai.co.il",
            DisplayName = "Tester Two",
            KeyHash = new string('b', 64),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _context.Satellites.Add(sat);
        _context.TleRecords.Add(tle);
        _context.ApiKeys.AddRange(apiKey1, apiKey2);
        _context.SaveChanges();
        return (sat, tle, apiKey1, apiKey2);
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
    public async Task PatchNotify_UnknownPassId_ReturnsNotFound()
    {
        var controller = BuildController(_subscriptionRepo, pass: null, Guid.NewGuid());

        var result = await controller.PatchNotify(Guid.NewGuid(), new PatchNotifyRequest(false));

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task PatchNotify_NotifyFalse_CreatesOptOutRowAndEffectiveStatusIsFalse()
    {
        var (sat, tle, apiKey1, _) = Seed();
        var pass = MakePass(sat.Id, tle.Id);
        _context.Passes.Add(pass);
        await _context.SaveChangesAsync();

        var controller = BuildController(_subscriptionRepo, pass, apiKey1.Id);

        var result = await controller.PatchNotify(pass.Id, new PatchNotifyRequest(false));

        Assert.IsType<OkObjectResult>(result);
        var effective = await _subscriptionRepo.GetEffectiveNotifyStatusAsync(pass.Id, apiKey1.Id);
        Assert.False(effective.Value);
    }

    [Fact]
    public async Task PatchNotify_NotifyTrueAfterFalse_RestoresDefaultAndRemovesRow()
    {
        var (sat, tle, apiKey1, _) = Seed();
        var pass = MakePass(sat.Id, tle.Id);
        _context.Passes.Add(pass);
        await _context.SaveChangesAsync();

        await _subscriptionRepo.SetNotifyAsync(pass.Id, apiKey1.Id, notify: false);

        var controller = BuildController(_subscriptionRepo, pass, apiKey1.Id);
        var result = await controller.PatchNotify(pass.Id, new PatchNotifyRequest(true));

        Assert.IsType<OkObjectResult>(result);
        var effective = await _subscriptionRepo.GetEffectiveNotifyStatusAsync(pass.Id, apiKey1.Id);
        Assert.True(effective.Value);
        Assert.Empty(_context.PassSubscriptions.Where(s => s.PassId == pass.Id && s.ApiKeyId == apiKey1.Id));
    }

    [Fact]
    public async Task PatchNotify_DoesNotAffectOtherTestersSubscriptionForSamePass()
    {
        var (sat, tle, apiKey1, apiKey2) = Seed();
        var pass = MakePass(sat.Id, tle.Id);
        _context.Passes.Add(pass);
        await _context.SaveChangesAsync();

        await _subscriptionRepo.SetNotifyAsync(pass.Id, apiKey2.Id, notify: false);

        var controller = BuildController(_subscriptionRepo, pass, apiKey1.Id);
        await controller.PatchNotify(pass.Id, new PatchNotifyRequest(false));

        var tester1Effective = await _subscriptionRepo.GetEffectiveNotifyStatusAsync(pass.Id, apiKey1.Id);
        var tester2Effective = await _subscriptionRepo.GetEffectiveNotifyStatusAsync(pass.Id, apiKey2.Id);
        Assert.False(tester1Effective.Value);
        Assert.False(tester2Effective.Value);
    }

    // ── GetTrack ──────────────────────────────────────────────────────────────
    // These exercise only the controller's routing/caching behavior with IPassService mocked —
    // the real SGP4-backed track computation (correct TLE selection, point spanning) is covered
    // at the service level in PassServiceTests.GetPassTrackAsync_* instead.

    private static readonly IReadOnlyList<GroundTrackPoint> SampleTrack =
    [
        new GroundTrackPoint(32.0, 34.0, 400.0, DateTime.UtcNow),
        new GroundTrackPoint(33.0, 35.0, 410.0, DateTime.UtcNow.AddSeconds(10))
    ];

    [Fact]
    public async Task GetTrack_UnknownPassId_ReturnsNotFound()
    {
        var (controller, passServiceMock) = BuildControllerWithMock(_subscriptionRepo, pass: null, Guid.NewGuid());
        passServiceMock
            .Setup(s => s.GetPassTrackAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Result<IEnumerable<GroundTrackPoint>>.Failure("Pass not found."));

        var result = await controller.GetTrack(Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetTrack_ValidPassId_ReturnsOkWithPoints()
    {
        var passId = Guid.NewGuid();
        var (controller, passServiceMock) = BuildControllerWithMock(_subscriptionRepo, pass: null, Guid.NewGuid());
        passServiceMock
            .Setup(s => s.GetPassTrackAsync(passId))
            .ReturnsAsync(Result<IEnumerable<GroundTrackPoint>>.Success(SampleTrack));

        var result = await controller.GetTrack(passId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<PassTrackDto>(ok.Value);
        Assert.Equal(passId, dto.PassId);
        Assert.Equal(SampleTrack.Count, dto.Points.Count);
    }

    [Fact]
    public async Task GetTrack_CalledTwiceForSamePassId_OnlyComputesOnce()
    {
        var passId = Guid.NewGuid();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var (controller, passServiceMock) = BuildControllerWithMock(_subscriptionRepo, pass: null, Guid.NewGuid(), cache);
        passServiceMock
            .Setup(s => s.GetPassTrackAsync(passId))
            .ReturnsAsync(Result<IEnumerable<GroundTrackPoint>>.Success(SampleTrack));

        await controller.GetTrack(passId);
        await controller.GetTrack(passId);

        passServiceMock.Verify(s => s.GetPassTrackAsync(passId), Times.Once);
    }
}
