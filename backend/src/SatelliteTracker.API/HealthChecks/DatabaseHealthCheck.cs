using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SatelliteTracker.API.HealthChecks;

// SELECT 1, without the retry strategy (see NpgsqlHealthDatabaseProbe).
public sealed class DatabaseHealthCheck : BoundedHealthCheck
{
    private readonly IHealthDatabaseProbe _probe;

    public DatabaseHealthCheck(IHealthDatabaseProbe probe, ILogger<DatabaseHealthCheck> logger) : base(logger)
    {
        _probe = probe;
    }

    protected override string Subject => "database probe";

    protected override async Task<HealthCheckResult> RunAsync(HealthCheckContext context, CancellationToken ct)
    {
        await _probe.PingAsync(ct);
        return HealthCheckResult.Healthy("database answered");
    }
}
