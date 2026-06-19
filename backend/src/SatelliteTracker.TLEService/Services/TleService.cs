using System.Globalization;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.TLEService.Client;

namespace SatelliteTracker.TLEService.Services;

// This class implements the ITleService interface to provide functionality for fetching, saving, and managing Two-Line Element (TLE) data for satellites.
public class TleService : ITleService
{
    private readonly IN2YOClient _n2yoClient; // Client for interacting with the N2YO API to fetch TLE data.
    private readonly ITleRepository _tleRepo; // Repository for managing TLE records in the database.
    private readonly ISatelliteRepository _satelliteRepo; // Repository for managing satellite records in the database.

    private static readonly TimeSpan TleTtl = TimeSpan.FromHours(2); // Time-to-live for TLE data, after which it is considered stale and should be refreshed.

    // Constructor that initializes the dependencies for the TLE service, including the N2YO client, TLE repository, and satellite repository.
    public TleService(IN2YOClient n2yoClient, ITleRepository tleRepo, ISatelliteRepository satelliteRepo)
    {
        _n2yoClient = n2yoClient;
        _tleRepo = tleRepo;
        _satelliteRepo = satelliteRepo;
    }
    // This method fetches the latest TLE data for a satellite given its NORAD ID, saves it to the database, and returns the saved TLE record.
    public async Task<Result<TleRecord>> FetchAndSaveAsync(int noradId, CancellationToken ct = default)
    {

        var satelliteResult = await _satelliteRepo.GetByNoradIdAsync(noradId);
        if (!satelliteResult.IsSuccess)
            return Result<TleRecord>.Failure(satelliteResult.Error!);

        // Fetch the TLE data from the N2YO API using the provided NORAD ID and cancellation token.
        var tleResult = await _n2yoClient.GetTleAsync(noradId, ct);

        if (!tleResult.IsSuccess)
            return Result<TleRecord>.Failure(tleResult.Error!);

        // The TLE data is expected to be in a format with two lines. We split the TLE string into lines and validate that we have at least two lines of data.
        var lines = tleResult.Value!.Tle.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // If there are fewer than 2 lines, the TLE data is considered invalid, and we return a failure result with an appropriate error message.
        if (lines.Length < 2)
            return Result<TleRecord>.Failure("Invalid TLE data: expected 2 lines.");

        var line1 = lines[0].Trim();
        var line2 = lines[1].Trim();

        // We create a new TleRecord object to represent the TLE data for the satellite. This includes generating a new GUID for the record ID, associating it with the satellite ID,
        // storing the TLE lines, parsing the epoch from the first line, and recording the current UTC time as the fetch time.
        var record = new TleRecord
        {
            Id = Guid.NewGuid(),
            SatelliteId = satelliteResult.Value!.Id,
            Line1 = line1,
            Line2 = line2,
            Epoch = ParseTleEpoch(line1),
            FetchedAt = DateTime.UtcNow
        };

        return await _tleRepo.AddAsync(record);
    }

    // This method retrieves the latest TLE record for a satellite given its NORAD ID.
    // It returns a Result object containing the TLE record if successful, or an error message if not.
    public async Task<Result<TleRecord>> GetLatestAsync(int noradId)
        => await _tleRepo.GetLatestByNoradIdAsync(noradId);

    public async Task<Result<bool>> IsTleStaleAsync(int noradId)
    {
        var result = await _tleRepo.GetLatestByNoradIdAsync(noradId);
        if (!result.IsSuccess)
            return Result<bool>.Success(true); // no record ⇒ treat as stale

        var isStale = DateTime.UtcNow - result.Value!.FetchedAt > TleTtl;
        return Result<bool>.Success(isStale);
    }

    // This private helper method parses the epoch date and time from the first line of the TLE data.
    // The epoch is represented in a specific format within the TLE line, and this method extracts and converts it to a DateTime object.
    private static DateTime ParseTleEpoch(string line1)
    {
        try
        {
            var epochStr = line1.Substring(18, 14).Trim();
            var year2 = int.Parse(epochStr[..2]);
            var dayFraction = double.Parse(epochStr[2..], CultureInfo.InvariantCulture);
            int year = year2 >= 57 ? 1900 + year2 : 2000 + year2;
            return new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(dayFraction - 1); 
        }
        catch
        {
            return DateTime.UtcNow;
        }
    }
}
