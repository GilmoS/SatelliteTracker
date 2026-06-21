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
        var settingsRepo = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();
        var passRepo = scope.ServiceProvider.GetRequiredService<IPassRepository>();
        var firebaseService = scope.ServiceProvider.GetRequiredService<IFirebaseService>();
        await ProcessPendingNotificationsAsync(settingsRepo, passRepo, firebaseService);
    }

    public async Task ProcessPendingNotificationsAsync(
        ISettingsRepository settingsRepo,
        IPassRepository passRepo,
        IFirebaseService firebaseService)
    {
        var settingsResult = await settingsRepo.GetAsync();
        if (!settingsResult.IsSuccess)
        {
            _logger.LogWarning("Failed to get settings: {Error}", settingsResult.Error);
            return;
        }

        var settings = settingsResult.Value!;
        if (string.IsNullOrEmpty(settings.FcmToken))
        {
            _logger.LogWarning("FCM token not configured — skipping pass notification check");
            return;
        }

        var passesResult = await passRepo.GetPendingNotificationsAsync();
        if (!passesResult.IsSuccess)
        {
            _logger.LogWarning("Failed to get pending notification passes: {Error}", passesResult.Error);
            return;
        }

        var now = DateTime.UtcNow;
        var alertMinutes = settings.AlertMinutes.OrderByDescending(m => m).ToArray();
        if (alertMinutes.Length == 0) return;
        var smallestAlert = alertMinutes.Min();

        foreach (var pass in passesResult.Value!)
        {
            var minutesUntilAos = (pass.Aos - now).TotalMinutes;

            foreach (var minutesBefore in alertMinutes)
            {
                if (Math.Abs(minutesUntilAos - minutesBefore) <= 1.0)
                {
                    await firebaseService.SendPassNotificationAsync(
                        settings.FcmToken,
                        pass.Satellite.Name,
                        pass.Aos,
                        minutesBefore);

                    if (minutesBefore == smallestAlert)
                    {
                        pass.NotificationSent = true;
                        pass.NotificationSentAt = now;
                        await passRepo.UpdateAsync(pass);
                    }

                    break;
                }
            }
        }
    }
}
