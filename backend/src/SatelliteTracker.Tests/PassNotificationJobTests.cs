using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SatelliteTracker.API.Jobs;
using SatelliteTracker.API.Services;
using SatelliteTracker.Database;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.Tests.Database.Helpers;
using Xunit;

namespace SatelliteTracker.Tests;

public class PassNotificationJobTests : IDisposable
{
    private readonly Mock<ILogger<PassNotificationJob>> _logger = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactory = new();

    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly PassRepository _passRepo;

    public PassNotificationJobTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _passRepo = new PassRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private PassNotificationJob CreateJob() => new(_logger.Object, _scopeFactory.Object);

    private static Settings MakeSettings(string? fcmToken = "test-token", int[]? alertMinutes = null) => new()
    {
        Id = Guid.NewGuid(),
        FcmToken = fcmToken,
        AlertMinutes = alertMinutes ?? [5, 10, 30],
        OutlookDays = 7,
        MinElevation = 5,
        UpdatedAt = DateTime.UtcNow
    };

    private static (Pass pass, Satellite satellite) MakePass(double minutesFromNow, bool notify = true, bool notificationSent = false)
    {
        var satellite = new Satellite
        {
            Id = Guid.NewGuid(),
            Name = "EROS C3",
            NoradId = 54880,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var pass = new Pass
        {
            Id = Guid.NewGuid(),
            SatelliteId = satellite.Id,
            Satellite = satellite,
            TleId = Guid.NewGuid(),
            Aos = DateTime.UtcNow.AddMinutes(minutesFromNow),
            Los = DateTime.UtcNow.AddMinutes(minutesFromNow + 10),
            MaxElevation = 45,
            AosAzimuth = 90,
            LosAzimuth = 270,
            DurationSec = 600,
            Notify = notify,
            NotificationSent = notificationSent,
            CalculatedAt = DateTime.UtcNow
        };
        return (pass, satellite);
    }

    [Fact]
    public async Task SendNotification_WhenPassIsWithinAlertWindow_SendsFCM()
    {
        var settings = MakeSettings(fcmToken: "device-token", alertMinutes: [5, 10]);
        var (pass, _) = MakePass(minutesFromNow: 10);

        var mockSettingsRepo = new Mock<ISettingsRepository>();
        mockSettingsRepo.Setup(r => r.GetAsync())
            .ReturnsAsync(Result<Settings>.Success(settings));

        var mockPassRepo = new Mock<IPassRepository>();
        mockPassRepo.Setup(r => r.GetPendingNotificationsAsync())
            .ReturnsAsync(Result<IEnumerable<Pass>>.Success([pass]));
        mockPassRepo.Setup(r => r.UpdateAsync(It.IsAny<Pass>()))
            .ReturnsAsync((Pass p) => Result<Pass>.Success(p));

        var mockFirebase = new Mock<IFirebaseService>();
        mockFirebase.Setup(f => f.SendPassNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        await CreateJob().ProcessPendingNotificationsAsync(
            mockSettingsRepo.Object, mockPassRepo.Object, mockFirebase.Object);

        mockFirebase.Verify(f => f.SendPassNotificationAsync(
            "device-token", "EROS C3", pass.Aos, 10), Times.Once);
    }

    [Fact]
    public async Task SendNotification_WhenFcmTokenIsEmpty_SkipsNotification()
    {
        var settings = MakeSettings(fcmToken: null);

        var mockSettingsRepo = new Mock<ISettingsRepository>();
        mockSettingsRepo.Setup(r => r.GetAsync())
            .ReturnsAsync(Result<Settings>.Success(settings));

        var mockPassRepo = new Mock<IPassRepository>();
        var mockFirebase = new Mock<IFirebaseService>();

        await CreateJob().ProcessPendingNotificationsAsync(
            mockSettingsRepo.Object, mockPassRepo.Object, mockFirebase.Object);

        mockFirebase.Verify(f => f.SendPassNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>()), Times.Never);
        mockPassRepo.Verify(r => r.GetPendingNotificationsAsync(), Times.Never);
    }

    [Fact]
    public async Task SendNotification_WhenPassAlreadySent_SkipsNotification()
    {
        var settings = MakeSettings(alertMinutes: [5, 10]);

        var mockSettingsRepo = new Mock<ISettingsRepository>();
        mockSettingsRepo.Setup(r => r.GetAsync())
            .ReturnsAsync(Result<Settings>.Success(settings));

        var mockPassRepo = new Mock<IPassRepository>();
        mockPassRepo.Setup(r => r.GetPendingNotificationsAsync())
            .ReturnsAsync(Result<IEnumerable<Pass>>.Success([]));

        var mockFirebase = new Mock<IFirebaseService>();

        await CreateJob().ProcessPendingNotificationsAsync(
            mockSettingsRepo.Object, mockPassRepo.Object, mockFirebase.Object);

        mockFirebase.Verify(f => f.SendPassNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetPendingNotifications_ReturnsOnlyNotifyTruePasses()
    {
        var satellite = new Satellite
        {
            Id = Guid.NewGuid(),
            Name = "EROS C3",
            NoradId = 54880,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Satellites.Add(satellite);

        var tle = new TleRecord
        {
            Id = Guid.NewGuid(),
            SatelliteId = satellite.Id,
            Line1 = "1 54880U 22099A   23001.00000000  .00000000  00000-0  00000-0 0  9999",
            Line2 = "2 54880  97.7000 208.9163 0001382  95.6617 344.7248 14.98120000303249",
            Epoch = DateTime.UtcNow,
            FetchedAt = DateTime.UtcNow
        };
        _context.TleRecords.Add(tle);

        var notifyPass = new Pass
        {
            Id = Guid.NewGuid(),
            SatelliteId = satellite.Id,
            TleId = tle.Id,
            Notify = true,
            NotificationSent = false,
            Aos = DateTime.UtcNow.AddHours(1),
            Los = DateTime.UtcNow.AddHours(1).AddMinutes(10),
            MaxElevation = 45,
            AosAzimuth = 90,
            LosAzimuth = 270,
            DurationSec = 600,
            CalculatedAt = DateTime.UtcNow
        };

        var noNotifyPass = new Pass
        {
            Id = Guid.NewGuid(),
            SatelliteId = satellite.Id,
            TleId = tle.Id,
            Notify = false,
            NotificationSent = false,
            Aos = DateTime.UtcNow.AddHours(2),
            Los = DateTime.UtcNow.AddHours(2).AddMinutes(10),
            MaxElevation = 45,
            AosAzimuth = 90,
            LosAzimuth = 270,
            DurationSec = 600,
            CalculatedAt = DateTime.UtcNow
        };

        var alreadySentPass = new Pass
        {
            Id = Guid.NewGuid(),
            SatelliteId = satellite.Id,
            TleId = tle.Id,
            Notify = true,
            NotificationSent = true,
            NotificationSentAt = DateTime.UtcNow.AddMinutes(-5),
            Aos = DateTime.UtcNow.AddHours(3),
            Los = DateTime.UtcNow.AddHours(3).AddMinutes(10),
            MaxElevation = 45,
            AosAzimuth = 90,
            LosAzimuth = 270,
            DurationSec = 600,
            CalculatedAt = DateTime.UtcNow
        };

        _context.Passes.AddRange(notifyPass, noNotifyPass, alreadySentPass);
        await _context.SaveChangesAsync();

        var result = await _passRepo.GetPendingNotificationsAsync();

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal(notifyPass.Id, result.Value!.First().Id);
    }
}
