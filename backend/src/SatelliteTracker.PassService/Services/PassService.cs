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
    private readonly TimeProvider _timeProvider; // Clock for the calculation window, injectable for tests


    // Constructor that initializes the repositories and logger through dependency injection.
    public PassService(ISatelliteRepository satelliteRepo,ITleRepository tleRepo,IPassRepository passRepo,ILogger<PassService> logger , IOptions<ObserverSettings> observer, TimeProvider timeProvider)
    {
        _satelliteRepo = satelliteRepo;
        _tleRepo = tleRepo;
        _passRepo = passRepo;
        _logger = logger;
        _observer = observer.Value;
        _timeProvider = timeProvider;
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

        // One "now" for the whole calculation: it is both the prediction window start and the
        // boundary between upcoming passes (this job's to manage) and past/in-progress ones.
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Use the PassPredictor to calculate the upcoming passes of the satellite based on the TLE data and observer's location
        var passResults = PassPredictor.PredictPasses(
            tleData,
            satelliteId,
            _observer.Lat,
            _observer.Lng,
            _observer.AltMeters,
            now,
            now.AddDays(7),
            _observer.MinElevationDeg)
            // A pass already in progress at "now" comes back with its AOS clamped to the window
            // start, so its times are wrong. It isn't upcoming either, so it is left alone.
            .Where(pr => pr.AOS > now)
            .ToList();

        // No early return for an empty result: the upsert below still has to delete upcoming passes
        // that are no longer predicted.

        // Orbit number = TLE revolution number + ascending nodes up to AOS (see OrbitNumberCalculator)
        var orbitNumbers = OrbitNumberCalculator.ComputeRevolutionNumbers(
            tleData, passResults.Select(pr => pr.AOS).ToList());

        // Map the predicted pass results to Pass entities for saving to the database
        var passes = passResults.Select((pr, i) => new Pass
        {
            Id = Guid.NewGuid(),
            SatelliteId = satelliteId,
            TleId = tleRecord.Id,
            OrbitNumber = orbitNumbers[i],
            Aos = pr.AOS,
            Los = pr.LOS,
            MaxElevation = (decimal)pr.MaxElevation,
            AosAzimuth = (decimal)pr.AosAzimuth,
            LosAzimuth = (decimal)pr.LosAzimuth,
            DurationSec = pr.DurationSeconds,
            OutlookSynced = false,
            CalculatedAt = now
        }).ToList();

        // Upsert by (SatelliteId, OrbitNumber): existing upcoming passes keep their Id (and their
        // notes/subscriptions/notification logs), so recalculation no longer churns pass IDs.
        var saveResult = await _passRepo.UpsertUpcomingAsync(satelliteId, now, passes);

        if (!saveResult.IsSuccess)
            return Result<IEnumerable<PassResult>>.Failure(saveResult.Error!);

        var upsert = saveResult.Value!;
        _logger.LogInformation(
            "Upserted passes for satellite {SatelliteId}: {Inserted} inserted, {Updated} updated, {Deleted} deleted, {SkippedPast} skipped (orbit already past)",
            satelliteId, upsert.Inserted, upsert.Updated, upsert.Deleted, upsert.SkippedPast);

        return Result<IEnumerable<PassResult>>.Success(passResults); // Return the predicted pass results as a successful result
    }

    // This method retrieves the upcoming passes of a satellite from the database.
    public async Task<Result<IEnumerable<Pass>>> GetUpcomingPassesAsync(Guid satelliteId)
        => await _passRepo.GetUpcomingAsync(satelliteId, DateTime.UtcNow, DateTime.UtcNow.AddDays(7));

    // This method retrieves the paginated, filterable pass history of a satellite from the
    // database for the past 6 months.
    public async Task<Result<PagedResult<Pass>>> GetPassHistoryAsync(Guid satelliteId, PassHistoryQuery query)
        => await _passRepo.GetHistoryAsync(satelliteId, DateTime.UtcNow.AddMonths(-6), query);

    // This method retrieves a specific pass by its ID from the database.
    public async Task<Result<Pass>> GetPassByIdAsync(Guid passId)
        => await _passRepo.GetByIdAsync(passId);

    // Computes the ground track for a specific pass, anchored to its stored TleId and [Aos, Los]
    // window — deliberately NOT the satellite's latest TLE, so the track stays consistent with the
    // AOS/LOS/max-elevation figures already shown to the user for this pass.
    public async Task<Result<IEnumerable<GroundTrackPoint>>> GetPassTrackAsync(Guid passId)
    {
        var passResult = await _passRepo.GetByIdAsync(passId);
        if (!passResult.IsSuccess)
            return Result<IEnumerable<GroundTrackPoint>>.Failure(passResult.Error!);

        var pass = passResult.Value!;

        var tleResult = await _tleRepo.GetByIdAsync(pass.TleId);
        if (!tleResult.IsSuccess)
            return Result<IEnumerable<GroundTrackPoint>>.Failure(tleResult.Error!);

        TleData tleData;
        try
        {
            tleData = TleParser.Parse(tleResult.Value!.Line1, tleResult.Value!.Line2);
        }
        catch (TleParseException ex)
        {
            return Result<IEnumerable<GroundTrackPoint>>.Failure($"TLE parse error: {ex.Message}");
        }

        var points = GroundTrackCalculator.ComputeGroundTrack(tleData, pass.Aos, pass.Los);
        return Result<IEnumerable<GroundTrackPoint>>.Success(points);
    }

    // 30 s between orbit-track points: roughly 230 km apart for a LEO satellite, smooth at world
    // zoom, and about 400 points for the two-period window.
    private const int OrbitTrackStepSeconds = 30;

    public async Task<Result<OrbitTrack>> GetOrbitTrackAsync(Guid satelliteId, DateTime nowUtc, TimeSpan extraAhead)
    {
        var satResult = await _satelliteRepo.GetByIdAsync(satelliteId);
        if (!satResult.IsSuccess)
            return Result<OrbitTrack>.Failure(satResult.Error!);

        // Unlike GetPassTrackAsync, this is a "now" view, so it uses the satellite's latest TLE.
        var tleResult = await _tleRepo.GetLatestByNoradIdAsync(satResult.Value!.NoradId);
        if (!tleResult.IsSuccess)
            return Result<OrbitTrack>.Failure(tleResult.Error!);

        TleData tleData;
        try
        {
            tleData = TleParser.Parse(tleResult.Value!.Line1, tleResult.Value!.Line2);
        }
        catch (TleParseException ex)
        {
            return Result<OrbitTrack>.Failure($"TLE parse error: {ex.Message}");
        }

        // MeanMotion is in revs/day, so 1440 / MeanMotion is the orbital period in minutes.
        double periodMinutes = 1440.0 / tleData.MeanMotion;
        var period = TimeSpan.FromMinutes(periodMinutes);
        var points = GroundTrackCalculator.ComputeGroundTrack(
            tleData, nowUtc - period, nowUtc + period + extraAhead, OrbitTrackStepSeconds);
        return Result<OrbitTrack>.Success(new OrbitTrack(satResult.Value!.NoradId, periodMinutes, points));
    }
}
