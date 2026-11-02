using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SatelliteTracker.API.HealthChecks;

// The latest pass calculation must be recent. Pass.CalculatedAt is rewritten on every matched or
// inserted row of the hourly upsert (PassRepository.UpsertUpcomingAsync), so MAX(CalculatedAt) moves
// with every run that predicts at least one pass. See "Health checks" in CLAUDE.md for the limits.
public sealed class PassFreshnessHealthCheck : BoundedHealthCheck
{
    private readonly IHealthDatabaseProbe _probe;
    private readonly TimeProvider _timeProvider;

    public PassFreshnessHealthCheck(IHealthDatabaseProbe probe, TimeProvider timeProvider, ILogger<PassFreshnessHealthCheck> logger)
        : base(logger)
    {
        _probe = probe;
        _timeProvider = timeProvider;
    }

    protected override string Subject => "pass freshness query";

    protected override async Task<HealthCheckResult> RunAsync(HealthCheckContext context, CancellationToken ct)
    {
        var latest = await _probe.GetLatestPassCalculationAsync(ct);
        if (latest is null)
            return new HealthCheckResult(context.Registration.FailureStatus, "no passes calculated yet");

        var age = _timeProvider.GetUtcNow().UtcDateTime - latest.Value;
        var reason = $"latest pass calculation is {FormatAge(age)} old";

        return age >= HealthCheckPolicy.PassMaxAge
            ? new HealthCheckResult(context.Registration.FailureStatus, reason)
            : HealthCheckResult.Healthy(reason);
    }
}
