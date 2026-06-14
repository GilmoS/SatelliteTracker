using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.TLEService.Services;

namespace SatelliteTracker.TLEService.Jobs;

// This class represents a background job that periodically updates the Two-Line Element (TLE) data for active satellites.
public class TleUpdateJob : BackgroundService
{
    private readonly ITleService _tleService;
    private readonly ISatelliteRepository _satelliteRepository;
    private readonly ITleRepository _tleRepository;
    private readonly ILogger<TleUpdateJob> _logger;

    private static readonly TimeSpan Interval = TimeSpan.FromHours(2); // Interval between TLE updates

    // Constructor that initializes the dependencies for the TLE update job, including the TLE service, satellite repository, TLE repository, and logger.
    public TleUpdateJob(ITleService tleService,ISatelliteRepository satelliteRepository,ITleRepository tleRepository,ILogger<TleUpdateJob> logger)
    {
        _tleService = tleService;
        _satelliteRepository = satelliteRepository;
        _tleRepository = tleRepository;
        _logger = logger;
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
        // Retrieve the list of active satellites from the satellite repository. If the retrieval fails, log a warning and exit the method.
        var satellitesResult = await _satelliteRepository.GetActiveAsync();

        if (!satellitesResult.IsSuccess)
        {
            _logger.LogWarning("Failed to retrieve active satellites: {Error}", satellitesResult.Error);
            return;
        }

        foreach (var satellite in satellitesResult.Value!)
        {
            var result = await _tleService.FetchAndSaveAsync(satellite.NoradId, ct);
            if (!result.IsSuccess)
                _logger.LogWarning("Failed to fetch TLE for NORAD {NoradId}: {Error}",satellite.NoradId, result.Error);
        }

        await _tleRepository.DeleteOlderThanAsync(DateTime.UtcNow.AddMonths(-6)); // Clean up old TLE records that are older than 6 months to maintain a manageable database size.
    }
}
