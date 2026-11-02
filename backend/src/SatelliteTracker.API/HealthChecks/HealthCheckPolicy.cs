using SatelliteTracker.PassService.Services;
using SatelliteTracker.TLEService.Jobs;

namespace SatelliteTracker.API.HealthChecks;

// The one place the health check limits are defined. Code constants, the same in every environment,
// like DatabaseRetryPolicy. The freshness limits are derived from the job intervals so they can't
// drift apart: stale means two scheduled runs were missed, plus an hour of slack for a slow run.
public static class HealthCheckPolicy
{
    public const string ReadyTag = "ready";

    // Per-check limit. A health check answers "right now", never "eventually".
    public static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan Slack = TimeSpan.FromHours(1);

    public static readonly TimeSpan TleMaxAge = TleUpdateJob.Interval * 2 + Slack; // 5 h

    public static readonly TimeSpan PassMaxAge = PassCalculationJob.Interval * 2 + Slack; // 3 h
}
