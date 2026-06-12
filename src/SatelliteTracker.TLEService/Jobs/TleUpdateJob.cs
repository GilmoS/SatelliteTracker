using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.TLEService.Services;

namespace SatelliteTracker.TLEService.Jobs;

public class TleUpdateJob : BackgroundService
{
    private readonly ITleService _tleService;
    private readonly ISatelliteRepository _satelliteRepository;
    private readonly ITleRepository _tleRepository;
    private readonly ILogger<TleUpdateJob> _logger;

    private static readonly TimeSpan Interval = TimeSpan.FromHours(2);

    public TleUpdateJob(
        ITleService tleService,
        ISatelliteRepository satelliteRepository,
        ITleRepository tleRepository,
        ILogger<TleUpdateJob> logger)
    {
        _tleService = tleService;
        _satelliteRepository = satelliteRepository;
        _tleRepository = tleRepository;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunUpdate(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TLE update job encountered an unexpected error");
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

    private async Task RunUpdate(CancellationToken ct)
    {
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
                _logger.LogWarning("Failed to fetch TLE for NORAD {NoradId}: {Error}",
                    satellite.NoradId, result.Error);
        }

        await _tleRepository.DeleteOlderThanAsync(DateTime.UtcNow.AddMonths(-6));
    }
}
