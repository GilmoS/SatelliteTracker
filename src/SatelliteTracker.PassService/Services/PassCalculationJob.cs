using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SatelliteTracker.Database.Repositories;

namespace SatelliteTracker.PassService.Services;

public sealed class PassCalculationJob : IHostedService, IDisposable
{
    private readonly ISatelliteRepository _satelliteRepo;
    private readonly IPassService _passService;
    private readonly ILogger<PassCalculationJob> _logger;
    private Timer? _timer;

    public PassCalculationJob(
        ISatelliteRepository satelliteRepo,
        IPassService passService,
        ILogger<PassCalculationJob> logger)
    {
        _satelliteRepo = satelliteRepo;
        _passService = passService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(RunAsync, null, TimeSpan.Zero, TimeSpan.FromHours(1));
        return Task.CompletedTask;
    }

    private async void RunAsync(object? state)
    {
        var result = await _satelliteRepo.GetActiveAsync();
        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to fetch active satellites: {Error}", result.Error);
            return;
        }

        foreach (var satellite in result.Value!)
        {
            var passResult = await _passService.CalculateAndSavePassesAsync(satellite.Id);
            if (passResult.IsSuccess)
                _logger.LogInformation("Calculated passes for {Name} (NORAD {NoradId})",
                    satellite.Name, satellite.NoradId);
            else
                _logger.LogError("Pass calculation failed for {Name} (NORAD {NoradId}): {Error}",
                    satellite.Name, satellite.NoradId, passResult.Error);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose() => _timer?.Dispose();
}
