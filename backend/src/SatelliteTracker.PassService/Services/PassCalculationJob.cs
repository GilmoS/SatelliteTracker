using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SatelliteTracker.Database.Repositories;

namespace SatelliteTracker.PassService.Services;

//This class represents a background job that calculates satellite passes at regular intervals.
public sealed class PassCalculationJob : IHostedService, IDisposable
{
    
    private readonly ILogger<PassCalculationJob> _logger; // Logger for logging information and errors
    private readonly IServiceScopeFactory _scopeFactory; 
    private Timer? _timer; // Timer for scheduling the job

    public PassCalculationJob(ILogger<PassCalculationJob> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(RunAsync, null, TimeSpan.Zero, TimeSpan.FromHours(1));
        return Task.CompletedTask;
    }

    private async void RunAsync(object? state)
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
            var passResult = await passService.CalculateAndSavePassesAsync(satellite.Id);
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
