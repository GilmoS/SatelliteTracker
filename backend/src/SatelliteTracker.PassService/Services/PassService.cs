using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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


    private readonly ISatelliteRepository _satelliteRepo; // Repository for accessing satellite data
    private readonly ITleRepository _tleRepo; // Repository for accessing TLE (Two-Line Element) data, which is used for satellite orbit prediction
    private readonly IPassRepository _passRepo; // Repository for accessing and storing satellite pass data
    private readonly ILogger<PassService> _logger; // Logger for logging information and errors
    private readonly ObserverSettings _observer;


    // Constructor that initializes the repositories and logger through dependency injection.
    public PassService(ISatelliteRepository satelliteRepo,ITleRepository tleRepo,IPassRepository passRepo,ILogger<PassService> logger , IOptions<ObserverSettings> observer)
    {
        _satelliteRepo = satelliteRepo;
        _tleRepo = tleRepo;
        _passRepo = passRepo;
        _logger = logger;
        _observer = observer.Value;
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
            _observer.Lat,
            _observer.Lng,
            _observer.AltMeters,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(7),
            _observer.MinElevationDeg).ToList();

        if (passResults.Count == 0) // If no passes are predicted, return an empty result
            return Result<IEnumerable<PassResult>>.Success(passResults);

        // Map the predicted pass results to Pass entities for saving to the database
        var passes = passResults.Select(pr => new Pass
        {
            Id = Guid.NewGuid(),
            SatelliteId = satelliteId,
            TleId = tleRecord.Id,
            OrbitNumber = ComputeOrbitNumber(tleData, pr.AOS),
            Aos = pr.AOS,
            Los = pr.LOS,
            MaxElevation = (decimal)pr.MaxElevation,
            AosAzimuth = (decimal)pr.AosAzimuth,
            LosAzimuth = (decimal)pr.LosAzimuth,
            DurationSec = pr.DurationSeconds,
            OutlookSynced = false,
            CalculatedAt = DateTime.UtcNow
        }).ToList();

        //Remove previosly predicted passes 
        await _passRepo.DeleteUpcomingAsync(satelliteId, DateTime.UtcNow);

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

    // Computes the revolution number at a given pass time by advancing the TLE's Revolution
    // Number at Epoch by however many full orbits elapse between the TLE epoch and the pass AOS.
    // MeanMotion is in revs/day, so 1440 / MeanMotion gives the orbital period in minutes.
    private static int ComputeOrbitNumber(TleData tle, DateTime passAosUtc)
    {
        double orbitalPeriodMinutes = 1440.0 / tle.MeanMotion;
        double elapsedMinutes = (passAosUtc - tle.Epoch).TotalMinutes;
        int elapsedOrbits = (int)Math.Floor(elapsedMinutes / orbitalPeriodMinutes);

        return tle.RevolutionNumber + elapsedOrbits;
    }
}
