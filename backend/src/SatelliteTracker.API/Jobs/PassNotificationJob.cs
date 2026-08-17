using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SatelliteTracker.API.Services;
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
        var firebaseService = scope.ServiceProvider.GetRequiredService<IFirebaseService>();
        await ProcessPendingNotificationsAsync(userSettingsRepo, passRepo, firebaseService);
    }

    public async Task ProcessPendingNotificationsAsync(
        IUserSettingsRepository userSettingsRepo,
        IPassRepository passRepo,
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

        var now = DateTime.UtcNow;

        // Once we've passed the closest alert boundary across ALL testers, no tester's
        // alert can still be pending — that's the point at which a pass is "done", since
        // GetPendingNotificationsAsync stops returning it once NotificationSent is set.
        var globalSmallestAlert = testerSettings
            .SelectMany(s => s.AlertMinutes)
            .DefaultIfEmpty(0)
            .Min();

        foreach (var pass in passesResult.Value!)
        {
            var minutesUntilAos = (pass.Aos - now).TotalMinutes;

            foreach (var settings in testerSettings)
            {
                var alertMinutes = settings.AlertMinutes;
                foreach (var minutesBefore in alertMinutes)
                {
                    if (Math.Abs(minutesUntilAos - minutesBefore) <= 1.0)
                    {
                        await firebaseService.SendPassNotificationAsync(
                            settings.FcmToken!,
                            pass.Satellite.Name,
                            pass.Aos,
                            minutesBefore);
                        break;
                    }
                }
            }

            if (Math.Abs(minutesUntilAos - globalSmallestAlert) <= 1.0)
            {
                pass.NotificationSent = true;
                pass.NotificationSentAt = now;
                await passRepo.UpdateAsync(pass);
            }
        }
    }
}
