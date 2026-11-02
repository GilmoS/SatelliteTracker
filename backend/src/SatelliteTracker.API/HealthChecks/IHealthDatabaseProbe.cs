namespace SatelliteTracker.API.HealthChecks;

// Every database read the health checks make. An abstraction so the checks can be tested without a
// real Postgres; NpgsqlHealthDatabaseProbe is the production implementation.
public interface IHealthDatabaseProbe
{
    // Throws if the database does not answer.
    Task PingAsync(CancellationToken ct);

    // One entry per active satellite, including those that have no TLE at all.
    Task<IReadOnlyList<SatelliteTleFetch>> GetLatestTleFetchPerActiveSatelliteAsync(CancellationToken ct);

    // Null when no pass has ever been calculated.
    Task<DateTime?> GetLatestPassCalculationAsync(CancellationToken ct);
}

public sealed record SatelliteTleFetch(string SatelliteName, DateTime? LatestFetchedAt);
