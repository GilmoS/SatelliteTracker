using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SatelliteTracker.API.Services;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;

namespace SatelliteTracker.API.Jobs;

public class PassNotificationJob : BackgroundService
{
    private readonly ILogger<PassNotificationJob> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    // Total sends per (pass, tester, threshold): the first attempt plus up to two retries, one per tick.
    public const int MaxAttempts = 3;

    public PassNotificationJob(ILogger<PassNotificationJob> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pass notification job encountered an unexpected error");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunTickAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var userSettingsRepo = scope.ServiceProvider.GetRequiredService<IUserSettingsRepository>();
        var passRepo = scope.ServiceProvider.GetRequiredService<IPassRepository>();
        var subscriptionRepo = scope.ServiceProvider.GetRequiredService<IPassSubscriptionRepository>();
        var notificationLogRepo = scope.ServiceProvider.GetRequiredService<IPassNotificationLogRepository>();
        var firebaseService = scope.ServiceProvider.GetRequiredService<IFirebaseService>();
        await ProcessPendingNotificationsAsync(
            userSettingsRepo, passRepo, subscriptionRepo, notificationLogRepo, firebaseService);
    }

    public async Task ProcessPendingNotificationsAsync(
        IUserSettingsRepository userSettingsRepo,
        IPassRepository passRepo,
        IPassSubscriptionRepository subscriptionRepo,
        IPassNotificationLogRepository notificationLogRepo,
        IFirebaseService firebaseService)
    {
        var settingsResult = await userSettingsRepo.GetAllActiveAsync();
        if (!settingsResult.IsSuccess)
        {
            _logger.LogWarning("Failed to get user settings: {Error}", settingsResult.Error);
            return;
        }

        var testerSettings = settingsResult.Value!
            .Where(s => !string.IsNullOrEmpty(s.FcmToken))
            .ToList();

        if (testerSettings.Count == 0)
        {
            _logger.LogWarning("No active testers with an FCM token configured — skipping pass notification check");
            return;
        }

        var passesResult = await passRepo.GetPendingNotificationsAsync();
        if (!passesResult.IsSuccess)
        {
            _logger.LogWarning("Failed to get pending notification passes: {Error}", passesResult.Error);
            return;
        }

        var passes = passesResult.Value!.ToList();
        if (passes.Count == 0)
            return;

        var passIds = passes.Select(p => p.Id).ToList();

        var subscriptionsResult = await subscriptionRepo.GetByPassIdsAsync(passIds);
        if (!subscriptionsResult.IsSuccess)
        {
            _logger.LogWarning("Failed to get pass subscriptions: {Error}", subscriptionsResult.Error);
            return;
        }

        // Sparse opt-in table: only pairs present here have ever been toggled. Any (pass, tester)
        // pair missing from this dictionary defaults to notify = false.
        var notifyOverrides = subscriptionsResult.Value!
            .ToDictionary(s => (s.PassId, s.ApiKeyId), s => s.Notify);

        var sentLogResult = await notificationLogRepo.GetByPassIdsAsync(passIds);
        if (!sentLogResult.IsSuccess)
        {
            _logger.LogWarning("Failed to get pass notification log: {Error}", sentLogResult.Error);
            return;
        }

        var attempts = sentLogResult.Value!
            .ToDictionary(l => (l.PassId, l.ApiKeyId, l.AlertMinutes));

        var now = DateTime.UtcNow;

        // Testers whose token FCM rejected during this tick — skip their remaining sends.
        var deadTokenTesters = new HashSet<Guid>();

        foreach (var pass in passes)
        {
            var minutesUntilAos = (pass.Aos - now).TotalMinutes;

            foreach (var settings in testerSettings)
            {
                if (deadTokenTesters.Contains(settings.ApiKeyId))
                    continue;

                var notify = notifyOverrides.TryGetValue((pass.Id, settings.ApiKeyId), out var explicitNotify)
                    ? explicitNotify
                    : false;
                if (!notify)
                    continue;

                foreach (var minutesBefore in settings.AlertMinutes)
                {
                    var key = (pass.Id, settings.ApiKeyId, minutesBefore);
                    int attemptNumber;
                    int minutesInMessage;
                    Guid logId;

                    if (attempts.TryGetValue(key, out var existing))
                    {
                        // Retry a failed send while attempts remain and the pass hasn't started.
                        // The original threshold time has passed by now, so the message states
                        // the real minutes remaining instead of the threshold value.
                        if (existing.Status != PassNotificationStatus.Failed || existing.AttemptCount >= MaxAttempts)
                            continue;

                        minutesInMessage = (int)Math.Round(minutesUntilAos);
                        if (minutesInMessage < 1)
                            continue;

                        var claim = await notificationLogRepo.TryClaimRetryAsync(
                            existing.Id, existing.AttemptCount, now);
                        if (!claim.IsSuccess || !claim.Value)
                            continue;

                        attemptNumber = existing.AttemptCount + 1;
                        logId = existing.Id;
                    }
                    else
                    {
                        if (Math.Abs(minutesUntilAos - minutesBefore) > 1.0)
                            continue;

                        // Claim the threshold before sending, so a concurrent tick can't send it
                        // too; the row is updated with the real outcome below.
                        var log = new PassNotificationLog
                        {
                            Id = Guid.NewGuid(),
                            PassId = pass.Id,
                            ApiKeyId = settings.ApiKeyId,
                            AlertMinutes = minutesBefore,
                            Status = PassNotificationStatus.Pending,
                            AttemptCount = 1,
                            LastAttemptAt = now
                        };
                        var claim = await notificationLogRepo.TryInsertAsync(log);
                        if (!claim.IsSuccess)
                        {
                            _logger.LogWarning("Failed to claim notification for pass {PassId}: {Error}",
                                pass.Id, claim.Error);
                            continue;
                        }
                        if (!claim.Value)
                            continue;

                        attempts[key] = log;
                        attemptNumber = 1;
                        minutesInMessage = minutesBefore;
                        logId = log.Id;
                    }

                    var sendResult = await firebaseService.SendPassNotificationAsync(
                        settings.FcmToken!,
                        pass.Satellite.Name,
                        pass.Aos,
                        minutesInMessage,
                        pass.Id);

                    var status = sendResult.Outcome switch
                    {
                        FcmSendOutcome.Sent => PassNotificationStatus.Sent,
                        FcmSendOutcome.Failed when attemptNumber < MaxAttempts => PassNotificationStatus.Failed,
                        _ => PassNotificationStatus.Abandoned
                    };

                    var recorded = await notificationLogRepo.RecordOutcomeAsync(
                        logId, status, sendResult.Error, DateTimeOffset.UtcNow);
                    if (!recorded.IsSuccess)
                        _logger.LogWarning("Failed to record notification outcome {Status} for pass {PassId}: {Error}",
                            status, pass.Id, recorded.Error);

                    if (sendResult.Outcome == FcmSendOutcome.TokenInvalid)
                    {
                        deadTokenTesters.Add(settings.ApiKeyId);
                        var cleared = await userSettingsRepo.ClearFcmTokenAsync(settings.ApiKeyId, settings.FcmToken!);
                        if (!cleared.IsSuccess)
                            _logger.LogWarning("Failed to clear dead FCM token for tester {ApiKeyId}: {Error}",
                                settings.ApiKeyId, cleared.Error);
                        break;
                    }
                }
            }
        }
    }
}
