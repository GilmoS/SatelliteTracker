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

        // Sparse opt-out table: only pairs present here have ever been toggled. Any (pass, tester)
        // pair missing from this dictionary defaults to notify = true.
        var notifyOverrides = subscriptionsResult.Value!
            .ToDictionary(s => (s.PassId, s.ApiKeyId), s => s.Notify);

        var sentLogResult = await notificationLogRepo.GetByPassIdsAsync(passIds);
        if (!sentLogResult.IsSuccess)
        {
            _logger.LogWarning("Failed to get pass notification log: {Error}", sentLogResult.Error);
            return;
        }

        var alreadySent = sentLogResult.Value!
            .Select(l => (l.PassId, l.ApiKeyId, l.AlertMinutes))
            .ToHashSet();

        var now = DateTime.UtcNow;

        foreach (var pass in passes)
        {
            var minutesUntilAos = (pass.Aos - now).TotalMinutes;

            foreach (var settings in testerSettings)
            {
                var notify = notifyOverrides.TryGetValue((pass.Id, settings.ApiKeyId), out var explicitNotify)
                    ? explicitNotify
                    : true;
                if (!notify)
                    continue;

                foreach (var minutesBefore in settings.AlertMinutes)
                {
                    if (Math.Abs(minutesUntilAos - minutesBefore) > 1.0)
                        continue;

                    var key = (pass.Id, settings.ApiKeyId, minutesBefore);
                    if (alreadySent.Contains(key))
                        continue;

                    await firebaseService.SendPassNotificationAsync(
                        settings.FcmToken!,
                        pass.Satellite.Name,
                        pass.Aos,
                        minutesBefore);

                    // TryInsertAsync tolerates a concurrent job tick already having logged this
                    // key — either way, this threshold is now accounted for.
                    await notificationLogRepo.TryInsertAsync(new PassNotificationLog
                    {
                        Id = Guid.NewGuid(),
                        PassId = pass.Id,
                        ApiKeyId = settings.ApiKeyId,
                        AlertMinutes = minutesBefore,
                        SentAt = now
                    });
                    alreadySent.Add(key);
                }
            }
        }
    }
}
