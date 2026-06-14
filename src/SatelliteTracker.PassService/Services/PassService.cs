using Microsoft.Extensions.Logging;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.PassService.SGP4;

namespace SatelliteTracker.PassService.Services;

//This class implements the IPassService interface and provides methods for calculating and retrieving satellite passes.
public class PassService : IPassService
{
    // Constants for the observer's location and minimum elevation angle for pass prediction
    // These values are used to determine when a satellite pass is visible from the observer's location.
    //for now , the observer's location is hardcoded to a specific latitude, longitude, and altitude (Ben Gurion International Airport , Israel).
    private const double ObserverLat = 32.0055;
    private const double ObserverLng = 34.8854;
    private const double ObserverAltM = 135.0;
    private const double MinElevationDeg = 5.0;

    private readonly ISatelliteRepository _satelliteRepo; // Repository for accessing satellite data
    private readonly ITleRepository _tleRepo; // Repository for accessing TLE (Two-Line Element) data, which is used for satellite orbit prediction
    private readonly IPassRepository _passRepo; // Repository for accessing and storing satellite pass data
    private readonly ILogger<PassService> _logger; // Logger for logging information and errors


    // Constructor that initializes the repositories and logger through dependency injection.
    public PassService(ISatelliteRepository satelliteRepo,ITleRepository tleRepo,IPassRepository passRepo,ILogger<PassService> logger)
    {
        _satelliteRepo = satelliteRepo;
        _tleRepo = tleRepo;
        _passRepo = passRepo;
        _logger = logger;
    }

    // This method calculates the upcoming passes of a satellite and saves them to the database.
    public async Task<Result<IEnumerable<PassResult>>> CalculateAndSavePassesAsync(Guid satelliteId, CancellationToken ct = default)
    {
        var satResult = await _satelliteRepo.GetByIdAsync(satelliteId); // Retrieve the satellite information from the repository using its ID

        if (!satResult.IsSuccess)
            return Result<IEnumerable<PassResult>>.Failure(satResult.Error!);

        var satellite = satResult.Value!; // Get the satellite entity from the result

        var tleResult = await _tleRepo.GetLatestByNoradIdAsync(satellite.NoradId); // Retrieve the latest TLE data for the satellite using its NORAD ID

        if (!tleResult.IsSuccess)
            return Result<IEnumerable<PassResult>>.Failure(tleResult.Error!);


        var tleRecord = tleResult.Value!; // Get the TLE record from the result

        TleData tleData; // Declare a variable to hold the parsed TLE data

        try
        {
            tleData = TleParser.Parse(tleRecord.Line1, tleRecord.Line2);
        }
        catch (TleParseException ex)
        {
            return Result<IEnumerable<PassResult>>.Failure($"TLE parse error: {ex.Message}");
        }

        // Use the PassPredictor to calculate the upcoming passes of the satellite based on the TLE data and observer's location
        var passResults = PassPredictor.PredictPasses(
            tleData,
            satelliteId,
            ObserverLat,
            ObserverLng,
            ObserverAltM,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(7),
            MinElevationDeg).ToList();

        if (passResults.Count == 0) // If no passes are predicted, return an empty result
            return Result<IEnumerable<PassResult>>.Success(passResults);

        // Map the predicted pass results to Pass entities for saving to the database
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

        // Save the predicted passes to the database using the pass repository
        var saveResult = await _passRepo.AddRangeAsync(passes);

        if (!saveResult.IsSuccess)
            return Result<IEnumerable<PassResult>>.Failure(saveResult.Error!);

        // Log the number of passes saved for the satellite
        _logger.LogInformation("Saved {Count} passes for satellite {SatelliteId}", passes.Count, satelliteId);

        return Result<IEnumerable<PassResult>>.Success(passResults); // Return the predicted pass results as a successful result
    }

    // This method retrieves the upcoming passes of a satellite from the database.
    public async Task<Result<IEnumerable<Pass>>> GetUpcomingPassesAsync(Guid satelliteId)
        => await _passRepo.GetUpcomingAsync(satelliteId, DateTime.UtcNow, DateTime.UtcNow.AddDays(7));

    // This method retrieves the pass history of a satellite from the database for the past 6 months.
    public async Task<Result<IEnumerable<Pass>>> GetPassHistoryAsync(Guid satelliteId)
        => await _passRepo.GetHistoryAsync(satelliteId, DateTime.UtcNow.AddMonths(-6));

    // This method retrieves a specific pass by its ID from the database.
    public async Task<Result<Pass>> GetPassByIdAsync(Guid passId)
        => await _passRepo.GetByIdAsync(passId);
}
