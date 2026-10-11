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

    private static UserSettings MakeUserSettings(
        Guid? apiKeyId = null, string? fcmToken = "test-token", int[]? alertMinutes = null) => new()
    {
        Id = Guid.NewGuid(),
        ApiKeyId = apiKeyId ?? Guid.NewGuid(),
        FcmToken = fcmToken,
        AlertMinutes = alertMinutes ?? [5, 10, 30],
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static (Pass pass, Satellite satellite) MakePass(double minutesFromNow)
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
            CalculatedAt = DateTime.UtcNow
        };
        return (pass, satellite);
    }

    private static Mock<IPassRepository> MockPassRepo(params Pass[] passes)
    {
        var mock = new Mock<IPassRepository>();
        mock.Setup(r => r.GetPendingNotificationsAsync())
            .ReturnsAsync(Result<IEnumerable<Pass>>.Success(passes));
        return mock;
    }

    private static PassSubscription MakeOptIn(Guid passId, Guid apiKeyId) => new()
    {
        Id = Guid.NewGuid(),
        PassId = passId,
        ApiKeyId = apiKeyId,
        Notify = true,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static Mock<IPassSubscriptionRepository> MockSubscriptionRepo(params PassSubscription[] subscriptions)
    {
        var mock = new Mock<IPassSubscriptionRepository>();
        mock.Setup(r => r.GetByPassIdsAsync(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(Result<IEnumerable<PassSubscription>>.Success(subscriptions));
        return mock;
    }

    private static Mock<IPassNotificationLogRepository> MockLogRepo(params PassNotificationLog[] alreadySent)
    {
        var mock = new Mock<IPassNotificationLogRepository>();
        mock.Setup(r => r.GetByPassIdsAsync(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(Result<IEnumerable<PassNotificationLog>>.Success(alreadySent));
        mock.Setup(r => r.TryInsertAsync(It.IsAny<PassNotificationLog>()))
            .ReturnsAsync(Result<bool>.Success(true));
        mock.Setup(r => r.TryClaimRetryAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(Result<bool>.Success(true));
        mock.Setup(r => r.RecordOutcomeAsync(
                It.IsAny<Guid>(), It.IsAny<PassNotificationStatus>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(Result.Success());
        return mock;
    }

    private static Mock<IFirebaseService> MockFirebase(FcmSendResult? result = null)
    {
        var mock = new Mock<IFirebaseService>();
        mock.Setup(f => f.SendPassNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<Guid>()))
            .ReturnsAsync(result ?? FcmSendResult.Sent);
        return mock;
    }

    private static Mock<IUserSettingsRepository> MockSettingsRepo(params UserSettings[] settings)
    {
        var mock = new Mock<IUserSettingsRepository>();
        mock.Setup(r => r.GetAllActiveAsync())
            .ReturnsAsync(Result<IEnumerable<UserSettings>>.Success(settings));
        mock.Setup(r => r.ClearFcmTokenAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(Result<bool>.Success(true));
        return mock;
    }

    private static PassNotificationLog MakeFailedLog(Pass pass, Guid apiKeyId, int alertMinutes, int attemptCount) => new()
    {
        Id = Guid.NewGuid(),
        PassId = pass.Id,
        ApiKeyId = apiKeyId,
        AlertMinutes = alertMinutes,
        Status = PassNotificationStatus.Failed,
        AttemptCount = attemptCount,
        LastAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        LastError = "UNAVAILABLE"
    };

    private static Task Run(
        Mock<IUserSettingsRepository> settingsRepo, Mock<IPassRepository> passRepo,
        Mock<IPassSubscriptionRepository> subscriptionRepo, Mock<IPassNotificationLogRepository> logRepo,
        Mock<IFirebaseService> firebase) =>
        new PassNotificationJob(new Mock<ILogger<PassNotificationJob>>().Object, new Mock<IServiceScopeFactory>().Object)
            .ProcessPendingNotificationsAsync(settingsRepo.Object, passRepo.Object, subscriptionRepo.Object, logRepo.Object, firebase.Object);

    // --- Retry + honest outcome recording ---

    [Fact]
    public async Task FailedSend_IsRecordedAsFailed_NotSent()
    {
        var settings = MakeUserSettings(fcmToken: "device-token", alertMinutes: [10]);
        var (pass, _) = MakePass(minutesFromNow: 10);
        var logRepo = MockLogRepo();
        var firebase = MockFirebase(FcmSendResult.Failed("UNAVAILABLE"));

        await Run(MockSettingsRepo(settings), MockPassRepo(pass),
            MockSubscriptionRepo(MakeOptIn(pass.Id, settings.ApiKeyId)), logRepo, firebase);

        logRepo.Verify(r => r.RecordOutcomeAsync(
            It.IsAny<Guid>(), PassNotificationStatus.Failed, "UNAVAILABLE", It.IsAny<DateTimeOffset>()), Times.Once);
        logRepo.Verify(r => r.RecordOutcomeAsync(
            It.IsAny<Guid>(), PassNotificationStatus.Sent, It.IsAny<string?>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    [Fact]
    public async Task FailedRow_IsRetried_WithRealMinutesRemaining()
    {
        var settings = MakeUserSettings(fcmToken: "device-token", alertMinutes: [10]);
        var (pass, _) = MakePass(minutesFromNow: 8); // the 10-minute threshold has passed
        var failed = MakeFailedLog(pass, settings.ApiKeyId, alertMinutes: 10, attemptCount: 1);
        var logRepo = MockLogRepo(failed);
        var firebase = MockFirebase();

        await Run(MockSettingsRepo(settings), MockPassRepo(pass),
            MockSubscriptionRepo(MakeOptIn(pass.Id, settings.ApiKeyId)), logRepo, firebase);

        logRepo.Verify(r => r.TryClaimRetryAsync(failed.Id, 1, It.IsAny<DateTimeOffset>()), Times.Once);
        firebase.Verify(f => f.SendPassNotificationAsync("device-token", "EROS C3", pass.Aos, 8, pass.Id), Times.Once);
        logRepo.Verify(r => r.RecordOutcomeAsync(
            failed.Id, PassNotificationStatus.Sent, null, It.IsAny<DateTimeOffset>()), Times.Once);
        logRepo.Verify(r => r.TryInsertAsync(It.IsAny<PassNotificationLog>()), Times.Never);
    }

    [Fact]
    public async Task FailedRetry_OnLastAttempt_IsAbandoned()
    {
        var settings = MakeUserSettings(fcmToken: "device-token", alertMinutes: [10]);
        var (pass, _) = MakePass(minutesFromNow: 8);
        var failed = MakeFailedLog(pass, settings.ApiKeyId, alertMinutes: 10,
            attemptCount: PassNotificationJob.MaxAttempts - 1);
        var logRepo = MockLogRepo(failed);

        await Run(MockSettingsRepo(settings), MockPassRepo(pass),
            MockSubscriptionRepo(MakeOptIn(pass.Id, settings.ApiKeyId)), logRepo,
            MockFirebase(FcmSendResult.Failed("UNAVAILABLE")));

        logRepo.Verify(r => r.RecordOutcomeAsync(
            failed.Id, PassNotificationStatus.Abandoned, "UNAVAILABLE", It.IsAny<DateTimeOffset>()), Times.Once);
    }

    [Fact]
    public async Task FailedRow_WithAttemptsExhausted_IsNotRetried()
    {
        var settings = MakeUserSettings(fcmToken: "device-token", alertMinutes: [10]);
        var (pass, _) = MakePass(minutesFromNow: 8);
        var failed = MakeFailedLog(pass, settings.ApiKeyId, alertMinutes: 10,
            attemptCount: PassNotificationJob.MaxAttempts);
        var logRepo = MockLogRepo(failed);
        var firebase = MockFirebase();

        await Run(MockSettingsRepo(settings), MockPassRepo(pass),
            MockSubscriptionRepo(MakeOptIn(pass.Id, settings.ApiKeyId)), logRepo, firebase);

        logRepo.Verify(r => r.TryClaimRetryAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>()), Times.Never);
        firebase.Verify(f => f.SendPassNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task FailedRow_LessThanAMinuteBeforeAos_IsNotRetried()
    {
        var settings = MakeUserSettings(fcmToken: "device-token", alertMinutes: [5]);
        var (pass, _) = MakePass(minutesFromNow: 0.4);
        var logRepo = MockLogRepo(MakeFailedLog(pass, settings.ApiKeyId, alertMinutes: 5, attemptCount: 1));
        var firebase = MockFirebase();

        await Run(MockSettingsRepo(settings), MockPassRepo(pass),
            MockSubscriptionRepo(MakeOptIn(pass.Id, settings.ApiKeyId)), logRepo, firebase);

        firebase.Verify(f => f.SendPassNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task RetryClaimLost_ToConcurrentTick_DoesNotSend()
    {
        var settings = MakeUserSettings(fcmToken: "device-token", alertMinutes: [10]);
        var (pass, _) = MakePass(minutesFromNow: 8);
        var logRepo = MockLogRepo(MakeFailedLog(pass, settings.ApiKeyId, alertMinutes: 10, attemptCount: 1));
        logRepo.Setup(r => r.TryClaimRetryAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(Result<bool>.Success(false));
        var firebase = MockFirebase();

        await Run(MockSettingsRepo(settings), MockPassRepo(pass),
            MockSubscriptionRepo(MakeOptIn(pass.Id, settings.ApiKeyId)), logRepo, firebase);

        firebase.Verify(f => f.SendPassNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task FirstClaimLost_ToConcurrentTick_DoesNotSend()
    {
        var settings = MakeUserSettings(fcmToken: "device-token", alertMinutes: [10]);
        var (pass, _) = MakePass(minutesFromNow: 10);
        var logRepo = MockLogRepo();
        logRepo.Setup(r => r.TryInsertAsync(It.IsAny<PassNotificationLog>()))
            .ReturnsAsync(Result<bool>.Success(false));
        var firebase = MockFirebase();

        await Run(MockSettingsRepo(settings), MockPassRepo(pass),
            MockSubscriptionRepo(MakeOptIn(pass.Id, settings.ApiKeyId)), logRepo, firebase);

        firebase.Verify(f => f.SendPassNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DeadToken_IsAbandonedAndCleared_AndTesterSkippedForRestOfTick()
    {
        var settings = MakeUserSettings(fcmToken: "dead-token", alertMinutes: [10]);
        var (passA, _) = MakePass(minutesFromNow: 10);
        var (passB, _) = MakePass(minutesFromNow: 10);
        var settingsRepo = MockSettingsRepo(settings);
        var logRepo = MockLogRepo();
        var firebase = MockFirebase(FcmSendResult.TokenInvalid("Unregistered: token not registered"));

        await Run(settingsRepo, MockPassRepo(passA, passB),
            MockSubscriptionRepo(MakeOptIn(passA.Id, settings.ApiKeyId), MakeOptIn(passB.Id, settings.ApiKeyId)),
            logRepo, firebase);

        firebase.Verify(f => f.SendPassNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<Guid>()), Times.Once);
        logRepo.Verify(r => r.RecordOutcomeAsync(
            It.IsAny<Guid>(), PassNotificationStatus.Abandoned, It.IsAny<string?>(), It.IsAny<DateTimeOffset>()), Times.Once);
        settingsRepo.Verify(r => r.ClearFcmTokenAsync(settings.ApiKeyId, "dead-token"), Times.Once);
    }

    [Fact]
    public async Task TransientFailure_DoesNotClearToken()
    {
        var settings = MakeUserSettings(fcmToken: "device-token", alertMinutes: [10]);
        var (pass, _) = MakePass(minutesFromNow: 10);
        var settingsRepo = MockSettingsRepo(settings);

        await Run(settingsRepo, MockPassRepo(pass),
            MockSubscriptionRepo(MakeOptIn(pass.Id, settings.ApiKeyId)), MockLogRepo(),
            MockFirebase(FcmSendResult.Failed("UNAVAILABLE")));

        settingsRepo.Verify(r => r.ClearFcmTokenAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SendNotification_WhenPassIsWithinAlertWindow_SendsFCM()
    {
        var settings = MakeUserSettings(fcmToken: "device-token", alertMinutes: [5, 10]);
        var (pass, _) = MakePass(minutesFromNow: 10);
        var optIn = MakeOptIn(pass.Id, settings.ApiKeyId);

        var mockSettingsRepo = new Mock<IUserSettingsRepository>();
        mockSettingsRepo.Setup(r => r.GetAllActiveAsync())
            .ReturnsAsync(Result<IEnumerable<UserSettings>>.Success([settings]));

        var mockPassRepo = MockPassRepo(pass);
        var mockSubscriptionRepo = MockSubscriptionRepo(optIn);
        var mockLogRepo = MockLogRepo();

        var mockFirebase = MockFirebase();

        await CreateJob().ProcessPendingNotificationsAsync(
            mockSettingsRepo.Object, mockPassRepo.Object, mockSubscriptionRepo.Object, mockLogRepo.Object, mockFirebase.Object);

        mockFirebase.Verify(f => f.SendPassNotificationAsync(
            "device-token", "EROS C3", pass.Aos, 10, pass.Id), Times.Once);
        mockLogRepo.Verify(r => r.TryInsertAsync(It.Is<PassNotificationLog>(
            l => l.PassId == pass.Id && l.ApiKeyId == settings.ApiKeyId && l.AlertMinutes == 10
                 && l.Status == PassNotificationStatus.Pending && l.AttemptCount == 1)), Times.Once);
        mockLogRepo.Verify(r => r.RecordOutcomeAsync(
            It.IsAny<Guid>(), PassNotificationStatus.Sent, null, It.IsAny<DateTimeOffset>()), Times.Once);
    }

    // The Data payload (passId, type) is built inside FirebaseService, which calls the FirebaseAdmin
    // SDK's static FirebaseMessaging.DefaultInstance directly — there's no injected wrapper, so the
    // actual Message.Data dictionary can't be inspected from a unit test. This verifies the one thing
    // that IS testable at this boundary: each pass's own Id is threaded through to
    // SendPassNotificationAsync, rather than a stale/hardcoded/swapped value, across two distinct passes.
    [Fact]
    public async Task SendNotification_PassesCorrectPassIdForDataPayload()
    {
        var settings = MakeUserSettings(fcmToken: "device-token", alertMinutes: [10]);
        var (passA, _) = MakePass(minutesFromNow: 10);
        var (passB, _) = MakePass(minutesFromNow: 10);
        var optInA = MakeOptIn(passA.Id, settings.ApiKeyId);
        var optInB = MakeOptIn(passB.Id, settings.ApiKeyId);

        var mockSettingsRepo = new Mock<IUserSettingsRepository>();
        mockSettingsRepo.Setup(r => r.GetAllActiveAsync())
            .ReturnsAsync(Result<IEnumerable<UserSettings>>.Success([settings]));

        var mockPassRepo = MockPassRepo(passA, passB);
        var mockSubscriptionRepo = MockSubscriptionRepo(optInA, optInB);
        var mockLogRepo = MockLogRepo();
        var mockFirebase = MockFirebase();

        await CreateJob().ProcessPendingNotificationsAsync(
            mockSettingsRepo.Object, mockPassRepo.Object, mockSubscriptionRepo.Object, mockLogRepo.Object, mockFirebase.Object);

        mockFirebase.Verify(f => f.SendPassNotificationAsync(
            "device-token", "EROS C3", passA.Aos, 10, passA.Id), Times.Once);
        mockFirebase.Verify(f => f.SendPassNotificationAsync(
            "device-token", "EROS C3", passB.Aos, 10, passB.Id), Times.Once);
    }

    [Fact]
    public async Task SendNotification_WhenFcmTokenIsEmpty_SkipsNotification()
    {
        var settings = MakeUserSettings(fcmToken: null);

        var mockSettingsRepo = new Mock<IUserSettingsRepository>();
        mockSettingsRepo.Setup(r => r.GetAllActiveAsync())
            .ReturnsAsync(Result<IEnumerable<UserSettings>>.Success([settings]));

        var mockPassRepo = new Mock<IPassRepository>();
        var mockSubscriptionRepo = new Mock<IPassSubscriptionRepository>();
        var mockLogRepo = new Mock<IPassNotificationLogRepository>();
        var mockFirebase = MockFirebase();

        await CreateJob().ProcessPendingNotificationsAsync(
            mockSettingsRepo.Object, mockPassRepo.Object, mockSubscriptionRepo.Object, mockLogRepo.Object, mockFirebase.Object);

        mockFirebase.Verify(f => f.SendPassNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
        mockPassRepo.Verify(r => r.GetPendingNotificationsAsync(), Times.Never);
    }

    [Fact]
    public async Task SendNotification_WhenNoPendingPasses_SkipsNotification()
    {
        var settings = MakeUserSettings(alertMinutes: [5, 10]);

        var mockSettingsRepo = new Mock<IUserSettingsRepository>();
        mockSettingsRepo.Setup(r => r.GetAllActiveAsync())
            .ReturnsAsync(Result<IEnumerable<UserSettings>>.Success([settings]));

        var mockPassRepo = MockPassRepo();
        var mockSubscriptionRepo = new Mock<IPassSubscriptionRepository>();
        var mockLogRepo = new Mock<IPassNotificationLogRepository>();
        var mockFirebase = MockFirebase();

        await CreateJob().ProcessPendingNotificationsAsync(
            mockSettingsRepo.Object, mockPassRepo.Object, mockSubscriptionRepo.Object, mockLogRepo.Object, mockFirebase.Object);

        mockFirebase.Verify(f => f.SendPassNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
        mockSubscriptionRepo.Verify(r => r.GetByPassIdsAsync(It.IsAny<IEnumerable<Guid>>()), Times.Never);
    }

    [Fact]
    public async Task MultipleTesters_DifferentAlertMinutes_BothNotifiedIndependently()
    {
        var (pass, _) = MakePass(minutesFromNow: 10);
        var testerA = MakeUserSettings(fcmToken: "token-a", alertMinutes: [10]);
        var testerB = MakeUserSettings(fcmToken: "token-b", alertMinutes: [10, 30]);

        var mockSettingsRepo = new Mock<IUserSettingsRepository>();
        mockSettingsRepo.Setup(r => r.GetAllActiveAsync())
            .ReturnsAsync(Result<IEnumerable<UserSettings>>.Success([testerA, testerB]));

        var mockPassRepo = MockPassRepo(pass);
        var mockSubscriptionRepo = MockSubscriptionRepo(
            MakeOptIn(pass.Id, testerA.ApiKeyId), MakeOptIn(pass.Id, testerB.ApiKeyId));
        var mockLogRepo = MockLogRepo();
        var mockFirebase = MockFirebase();

        await CreateJob().ProcessPendingNotificationsAsync(
            mockSettingsRepo.Object, mockPassRepo.Object, mockSubscriptionRepo.Object, mockLogRepo.Object, mockFirebase.Object);

        mockFirebase.Verify(f => f.SendPassNotificationAsync("token-a", "EROS C3", pass.Aos, 10, pass.Id), Times.Once);
        mockFirebase.Verify(f => f.SendPassNotificationAsync("token-b", "EROS C3", pass.Aos, 10, pass.Id), Times.Once);
    }

    [Fact]
    public async Task UnsubscribedTester_NoSend()
    {
        var (pass, _) = MakePass(minutesFromNow: 10);
        var settings = MakeUserSettings(fcmToken: "device-token", alertMinutes: [10]);

        var optOut = new PassSubscription
        {
            Id = Guid.NewGuid(),
            PassId = pass.Id,
            ApiKeyId = settings.ApiKeyId,
            Notify = false,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var mockSettingsRepo = new Mock<IUserSettingsRepository>();
        mockSettingsRepo.Setup(r => r.GetAllActiveAsync())
            .ReturnsAsync(Result<IEnumerable<UserSettings>>.Success([settings]));

        var mockPassRepo = MockPassRepo(pass);
        var mockSubscriptionRepo = MockSubscriptionRepo(optOut);
        var mockLogRepo = MockLogRepo();
        var mockFirebase = MockFirebase();

        await CreateJob().ProcessPendingNotificationsAsync(
            mockSettingsRepo.Object, mockPassRepo.Object, mockSubscriptionRepo.Object, mockLogRepo.Object, mockFirebase.Object);

        mockFirebase.Verify(f => f.SendPassNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
        mockLogRepo.Verify(r => r.TryInsertAsync(It.IsAny<PassNotificationLog>()), Times.Never);
    }

    [Fact]
    public async Task TesterWithNoSubscriptionRow_DefaultsToNotNotified()
    {
        var (pass, _) = MakePass(minutesFromNow: 10);
        var settings = MakeUserSettings(fcmToken: "device-token", alertMinutes: [10]);

        var mockSettingsRepo = new Mock<IUserSettingsRepository>();
        mockSettingsRepo.Setup(r => r.GetAllActiveAsync())
            .ReturnsAsync(Result<IEnumerable<UserSettings>>.Success([settings]));

        var mockPassRepo = MockPassRepo(pass);
        // No subscription rows at all for this pass — sparse table, absence means notify = false.
        var mockSubscriptionRepo = MockSubscriptionRepo();
        var mockLogRepo = MockLogRepo();
        var mockFirebase = MockFirebase();

        await CreateJob().ProcessPendingNotificationsAsync(
            mockSettingsRepo.Object, mockPassRepo.Object, mockSubscriptionRepo.Object, mockLogRepo.Object, mockFirebase.Object);

        mockFirebase.Verify(f => f.SendPassNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ThresholdAlreadyLogged_NotResent()
    {
        var (pass, _) = MakePass(minutesFromNow: 10);
        var settings = MakeUserSettings(fcmToken: "device-token", alertMinutes: [10]);

        var existingLog = new PassNotificationLog
        {
            Id = Guid.NewGuid(),
            PassId = pass.Id,
            ApiKeyId = settings.ApiKeyId,
            AlertMinutes = 10,
            Status = PassNotificationStatus.Sent,
            AttemptCount = 1,
            SentAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        var mockSettingsRepo = new Mock<IUserSettingsRepository>();
        mockSettingsRepo.Setup(r => r.GetAllActiveAsync())
            .ReturnsAsync(Result<IEnumerable<UserSettings>>.Success([settings]));

        var mockPassRepo = MockPassRepo(pass);
        var mockSubscriptionRepo = MockSubscriptionRepo(MakeOptIn(pass.Id, settings.ApiKeyId));
        var mockLogRepo = MockLogRepo(existingLog);
        var mockFirebase = MockFirebase();

        await CreateJob().ProcessPendingNotificationsAsync(
            mockSettingsRepo.Object, mockPassRepo.Object, mockSubscriptionRepo.Object, mockLogRepo.Object, mockFirebase.Object);

        mockFirebase.Verify(f => f.SendPassNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
        mockLogRepo.Verify(r => r.TryInsertAsync(It.IsAny<PassNotificationLog>()), Times.Never);
    }

    // Regression test for the bug this whole change fixes: previously, a single global
    // NotificationSent flag on Pass meant that once ANY tester's earliest threshold fired, the
    // pass was marked done and no other tester (including one that registered afterward) could
    // ever be notified about it. Completion is now tracked per (pass, tester, threshold), so a
    // new tester still gets their own notifications for thresholds no one has logged for them yet.
    [Fact]
    public async Task NewTesterAfterOtherTesterAlreadyLogged_StillGetsOwnNotification()
    {
        var (pass, _) = MakePass(minutesFromNow: 10);
        var existingTester = MakeUserSettings(fcmToken: "existing-token", alertMinutes: [10]);
        var newTester = MakeUserSettings(fcmToken: "new-token", alertMinutes: [10]);

        var existingLog = new PassNotificationLog
        {
            Id = Guid.NewGuid(),
            PassId = pass.Id,
            ApiKeyId = existingTester.ApiKeyId,
            AlertMinutes = 10,
            Status = PassNotificationStatus.Sent,
            AttemptCount = 1,
            SentAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        var mockSettingsRepo = new Mock<IUserSettingsRepository>();
        mockSettingsRepo.Setup(r => r.GetAllActiveAsync())
            .ReturnsAsync(Result<IEnumerable<UserSettings>>.Success([existingTester, newTester]));

        var mockPassRepo = MockPassRepo(pass);
        var mockSubscriptionRepo = MockSubscriptionRepo(
            MakeOptIn(pass.Id, existingTester.ApiKeyId), MakeOptIn(pass.Id, newTester.ApiKeyId));
        var mockLogRepo = MockLogRepo(existingLog);
        var mockFirebase = MockFirebase();

        await CreateJob().ProcessPendingNotificationsAsync(
            mockSettingsRepo.Object, mockPassRepo.Object, mockSubscriptionRepo.Object, mockLogRepo.Object, mockFirebase.Object);

        mockFirebase.Verify(f => f.SendPassNotificationAsync(
            "existing-token", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<Guid>()), Times.Never);
        mockFirebase.Verify(f => f.SendPassNotificationAsync(
            "new-token", "EROS C3", pass.Aos, 10, pass.Id), Times.Once);
    }

    [Fact]
    public async Task GetPendingNotifications_ReturnsOnlyFuturePasses()
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

        var futurePass = new Pass
        {
            Id = Guid.NewGuid(),
            SatelliteId = satellite.Id,
            TleId = tle.Id,
            OrbitNumber = 1,
            Aos = DateTime.UtcNow.AddHours(1),
            Los = DateTime.UtcNow.AddHours(1).AddMinutes(10),
            MaxElevation = 45,
            AosAzimuth = 90,
            LosAzimuth = 270,
            DurationSec = 600,
            CalculatedAt = DateTime.UtcNow
        };

        var pastPass = new Pass
        {
            Id = Guid.NewGuid(),
            SatelliteId = satellite.Id,
            TleId = tle.Id,
            OrbitNumber = 2,
            Aos = DateTime.UtcNow.AddHours(-3),
            Los = DateTime.UtcNow.AddHours(-3).AddMinutes(10),
            MaxElevation = 45,
            AosAzimuth = 90,
            LosAzimuth = 270,
            DurationSec = 600,
            CalculatedAt = DateTime.UtcNow
        };

        _context.Passes.AddRange(futurePass, pastPass);
        await _context.SaveChangesAsync();

        var result = await _passRepo.GetPendingNotificationsAsync();

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal(futurePass.Id, result.Value!.First().Id);
    }
}
