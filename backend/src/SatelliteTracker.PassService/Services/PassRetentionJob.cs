using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SatelliteTracker.Database.Repositories;

namespace SatelliteTracker.PassService.Services;

// Daily retention: deletes passes older than the 6-month history window (plus a 1-day grace),
// with their notes, subscriptions and notification logs (DB cascade). Runs once at startup, so a
// server that is down at 03:00 still catches up, then every day at 03:00 Israel time.
// TLEs left without passes are removed by TleUpdateJob's existing 6-month cleanup.
public sealed class PassRetentionJob : BackgroundService
{
    private static readonly TimeZoneInfo IsraelTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
    private static readonly TimeSpan RunAtLocalTime = TimeSpan.FromHours(3);

    private readonly ILogger<PassRetentionJob> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;

    public PassRetentionJob(ILogger<PassRetentionJob> logger, IServiceScopeFactory scopeFactory, TimeProvider timeProvider)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
    }

    // Passes with AOS before this are deleted: 6 months of history plus one day of grace.
    public static DateTime CutoffFor(DateTime utcNow) => utcNow.AddMonths(-6).AddDays(-1);

    // Time from utcNow until the next 03:00 in Asia/Jerusalem (DST-aware; 03:00 always exists
    // there because Israel's clock changes happen at 02:00).
    public static TimeSpan DelayUntilNextRun(DateTime utcNow)
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, IsraelTimeZone);
        var nextLocal = localNow.Date + RunAtLocalTime;
        if (nextLocal <= localNow)
            nextLocal = nextLocal.AddDays(1);

        return TimeZoneInfo.ConvertTimeToUtc(nextLocal, IsraelTimeZone) - utcNow;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var passRepo = scope.ServiceProvider.GetRequiredService<IPassRepository>();
                await RunOnceAsync(passRepo, _timeProvider.GetUtcNow().UtcDateTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pass retention job encountered an unexpected error");
            }

            try
            {
                var delay = DelayUntilNextRun(_timeProvider.GetUtcNow().UtcDateTime);
                await Task.Delay(delay, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task RunOnceAsync(IPassRepository passRepo, DateTime utcNow)
    {
        var cutoff = CutoffFor(utcNow);
        var result = await passRepo.DeleteAosBeforeAsync(cutoff);

        if (result.IsSuccess)
            _logger.LogInformation("Pass retention deleted {Count} passes with AOS before {Cutoff:u}",
                result.Value, cutoff);
        else
            _logger.LogError("Pass retention failed: {Error}", result.Error);
    }
}
