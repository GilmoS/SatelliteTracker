using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SatelliteTracker.Database.Repositories;

namespace SatelliteTracker.PassService.Services;

// Background job that recalculates passes for every active satellite: once at startup, then hourly.
// Each run is wrapped in try/catch so one failed run is logged and the next tick still happens;
// nothing here is async void, so an exception can never escape onto the thread pool and crash
// the process.
public sealed class PassCalculationJob : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly ILogger<PassCalculationJob> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;

    public PassCalculationJob(ILogger<PassCalculationJob> logger, IServiceScopeFactory scopeFactory, TimeProvider timeProvider)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // PeriodicTimer never overlaps runs: a tick that arrives while a run is still going is
        // delivered once that run finishes.
        using var timer = new PeriodicTimer(Interval, _timeProvider);

        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // shutdown, not an error
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pass calculation job encountered an unexpected error");
            }
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        var satelliteRepo = scope.ServiceProvider.GetRequiredService<ISatelliteRepository>();
        var passService = scope.ServiceProvider.GetRequiredService<IPassService>();

        var result = await satelliteRepo.GetActiveAsync();
        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to fetch active satellites: {Error}", result.Error);
            return;
        }

        foreach (var satellite in result.Value!)
        {
            ct.ThrowIfCancellationRequested();

            var passResult = await passService.CalculateAndSavePassesAsync(satellite.Id, ct);
            if (passResult.IsSuccess)
                _logger.LogInformation("Calculated passes for {Name} (NORAD {NoradId})",
                    satellite.Name, satellite.NoradId);
            else
                _logger.LogError("Pass calculation failed for {Name} (NORAD {NoradId}): {Error}",
                    satellite.Name, satellite.NoradId, passResult.Error);
        }
    }
}
