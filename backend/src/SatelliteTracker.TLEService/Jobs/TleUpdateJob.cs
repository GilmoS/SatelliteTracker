using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.TLEService.Services;

namespace SatelliteTracker.TLEService.Jobs;

// This class represents a background job that periodically updates the Two-Line Element (TLE) data for active satellites.
public class TleUpdateJob : BackgroundService
{
    
    private readonly ILogger<TleUpdateJob> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    private static readonly TimeSpan Interval = TimeSpan.FromHours(2); // Interval between TLE updates

    // Constructor that initializes the dependencies for the TLE update job, including the TLE service, satellite repository, TLE repository, and logger.
    public TleUpdateJob(ILogger<TleUpdateJob> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    // This method is called when the background service starts and runs the main loop for updating TLE data until the service is stopped.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested) // Main loop that continues until the service is stopped
        {
            try
            {
                await RunUpdate(stoppingToken); // Run the TLE update process, passing the cancellation token to allow for graceful shutdown
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) // If the operation was canceled due to the service being stopped, we break out of the loop without logging an error.
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TLE update job encountered an unexpected error");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken); // Wait for the specified interval before the next update, allowing for cancellation if the service is stopped during the delay.
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    // This method performs the actual TLE update process,
    // which includes retrieving active satellites, fetching and saving their TLE data, and cleaning up old TLE records.
    private async Task RunUpdate(CancellationToken ct)
    {
        
        using var scope = _scopeFactory.CreateScope();

        var tleService = scope.ServiceProvider.GetRequiredService<ITleService>();
        var satelliteRepo = scope.ServiceProvider.GetRequiredService<ISatelliteRepository>();
        var tleRepo = scope.ServiceProvider.GetRequiredService<ITleRepository>();

        var satellitesResult = await satelliteRepo.GetActiveAsync();
        if (!satellitesResult.IsSuccess)
        {
            _logger.LogWarning("Failed to retrieve active satellites: {Error}", satellitesResult.Error);
            return;
        }

        foreach (var satellite in satellitesResult.Value!)
        {
            var result = await tleService.FetchAndSaveAsync(satellite.NoradId, ct);
            if (!result.IsSuccess)
                _logger.LogWarning("Failed to fetch TLE for NORAD {NoradId}: {Error}",
                    satellite.NoradId, result.Error);
        }

        await tleRepo.DeleteOlderThanAsync(DateTime.UtcNow.AddMonths(-6));
    }
}

