using System.Globalization;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.TLEService.Client;

namespace SatelliteTracker.TLEService.Services;

public class TleService : ITleService
{
    private readonly IN2YOClient _n2yoClient;
    private readonly ITleRepository _tleRepo;
    private readonly ISatelliteRepository _satelliteRepo;

    private static readonly TimeSpan TleTtl = TimeSpan.FromHours(2);

    public TleService(IN2YOClient n2yoClient, ITleRepository tleRepo, ISatelliteRepository satelliteRepo)
    {
        _n2yoClient = n2yoClient;
        _tleRepo = tleRepo;
        _satelliteRepo = satelliteRepo;
    }

    public async Task<Result<TleRecord>> FetchAndSaveAsync(int noradId, CancellationToken ct = default)
    {
        var satelliteResult = await _satelliteRepo.GetByNoradIdAsync(noradId);
        if (!satelliteResult.IsSuccess)
            return Result<TleRecord>.Failure(satelliteResult.Error!);

        var tleResult = await _n2yoClient.GetTleAsync(noradId, ct);
        if (!tleResult.IsSuccess)
            return Result<TleRecord>.Failure(tleResult.Error!);

        var lines = tleResult.Value!.Tle.Split(
            new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2)
            return Result<TleRecord>.Failure("Invalid TLE data: expected 2 lines.");

        var line1 = lines[0].Trim();
        var line2 = lines[1].Trim();

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
