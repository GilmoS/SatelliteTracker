using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SatelliteTracker.API.HealthChecks;

// Every active satellite's latest TLE fetch must be recent. FetchedAt is written on every successful
// fetch, even of an unchanged TLE, so this also covers N2YO passively: health checks never call it,
// to protect the quota.
public sealed class TleFreshnessHealthCheck : BoundedHealthCheck
{
    private readonly IHealthDatabaseProbe _probe;
    private readonly TimeProvider _timeProvider;

    public TleFreshnessHealthCheck(IHealthDatabaseProbe probe, TimeProvider timeProvider, ILogger<TleFreshnessHealthCheck> logger)
        : base(logger)
    {
        _probe = probe;
        _timeProvider = timeProvider;
    }

    protected override string Subject => "TLE freshness query";

    protected override async Task<HealthCheckResult> RunAsync(HealthCheckContext context, CancellationToken ct)
    {
        var fetches = await _probe.GetLatestTleFetchPerActiveSatelliteAsync(ct);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var problems = new List<string>();
        foreach (var fetch in fetches)
        {
            if (fetch.LatestFetchedAt is null)
                problems.Add($"no TLE for {fetch.SatelliteName}");
            else if (now - fetch.LatestFetchedAt.Value >= HealthCheckPolicy.TleMaxAge)
                problems.Add($"latest TLE for {fetch.SatelliteName} is {FormatAge(now - fetch.LatestFetchedAt.Value)} old");
        }

        return problems.Count > 0
            ? new HealthCheckResult(context.Registration.FailureStatus, string.Join("; ", problems))
            : HealthCheckResult.Healthy(
                $"{fetches.Count} active satellite(s), all TLEs newer than {FormatAge(HealthCheckPolicy.TleMaxAge)}");
    }
}
