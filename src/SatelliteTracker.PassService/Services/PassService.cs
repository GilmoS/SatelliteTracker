using Microsoft.Extensions.Logging;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.PassService.SGP4;

namespace SatelliteTracker.PassService.Services;

public class PassService : IPassService
{
    private const double ObserverLat = 32.0055;
    private const double ObserverLng = 34.8854;
    private const double ObserverAltM = 135.0;
    private const double MinElevationDeg = 5.0;

    private readonly ISatelliteRepository _satelliteRepo;
    private readonly ITleRepository _tleRepo;
    private readonly IPassRepository _passRepo;
    private readonly ILogger<PassService> _logger;

    public PassService(
        ISatelliteRepository satelliteRepo,
        ITleRepository tleRepo,
        IPassRepository passRepo,
        ILogger<PassService> logger)
    {
        _satelliteRepo = satelliteRepo;
        _tleRepo = tleRepo;
        _passRepo = passRepo;
        _logger = logger;
    }

    public async Task<Result<IEnumerable<PassResult>>> CalculateAndSavePassesAsync(
        Guid satelliteId, CancellationToken ct = default)
    {
        var satResult = await _satelliteRepo.GetByIdAsync(satelliteId);
        if (!satResult.IsSuccess)
            return Result<IEnumerable<PassResult>>.Failure(satResult.Error!);

        var satellite = satResult.Value!;

        var tleResult = await _tleRepo.GetLatestByNoradIdAsync(satellite.NoradId);
        if (!tleResult.IsSuccess)
            return Result<IEnumerable<PassResult>>.Failure(tleResult.Error!);

        var tleRecord = tleResult.Value!;

        TleData tleData;
        try
        {
            tleData = TleParser.Parse(tleRecord.Line1, tleRecord.Line2);
        }
        catch (TleParseException ex)
        {
            return Result<IEnumerable<PassResult>>.Failure($"TLE parse error: {ex.Message}");
        }

        var passResults = PassPredictor.PredictPasses(
            tleData,
            satelliteId,
            ObserverLat,
            ObserverLng,
            ObserverAltM,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(7),
            MinElevationDeg).ToList();

        if (passResults.Count == 0)
            return Result<IEnumerable<PassResult>>.Success(passResults);

        var passes = passResults.Select(pr => new Pass
        {
            Id = Guid.NewGuid(),
            SatelliteId = satelliteId,
            TleId = tleRecord.Id,
            OrbitNumber = 0,
            Aos = pr.AOS,
            Los = pr.LOS,
            MaxElevation = (decimal)pr.MaxElevation,
            AosAzimuth = (decimal)pr.AosAzimuth,
            LosAzimuth = (decimal)pr.LosAzimuth,
            DurationSec = pr.DurationSeconds,
            NotificationSent = false,
            OutlookSynced = false,
            CalculatedAt = DateTime.UtcNow
        }).ToList();

        var saveResult = await _passRepo.AddRangeAsync(passes);
        if (!saveResult.IsSuccess)
            return Result<IEnumerable<PassResult>>.Failure(saveResult.Error!);

        _logger.LogInformation("Saved {Count} passes for satellite {SatelliteId}", passes.Count, satelliteId);
        return Result<IEnumerable<PassResult>>.Success(passResults);
    }

    public async Task<Result<IEnumerable<Pass>>> GetUpcomingPassesAsync(Guid satelliteId)
        => await _passRepo.GetUpcomingAsync(satelliteId, DateTime.UtcNow, DateTime.UtcNow.AddDays(7));

    public async Task<Result<IEnumerable<Pass>>> GetPassHistoryAsync(Guid satelliteId)
        => await _passRepo.GetHistoryAsync(satelliteId, DateTime.UtcNow.AddMonths(-6));

    public async Task<Result<Pass>> GetPassByIdAsync(Guid passId)
        => await _passRepo.GetByIdAsync(passId);
}
