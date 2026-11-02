using System.Globalization;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SatelliteTracker.API.HealthChecks;

// What every check shares: a hard time limit, and failure reasons written by our code.
//
// - HealthCheckRegistration.Timeout only cancels a token; the framework still awaits the check. A
//   driver call that ignores the token would hang the endpoint, so WaitAsync enforces the limit
//   whatever the call underneath does. An abandoned call finishes in the background.
// - The framework copies an uncaught exception's message into the report, so exceptions are caught
//   here. They go to the log; the report gets only a short reason that names no value.
public abstract class BoundedHealthCheck : IHealthCheck
{
    private readonly ILogger _logger;

    protected BoundedHealthCheck(ILogger logger)
    {
        _logger = logger;
    }

    // What is being checked, for the failure reasons, e.g. "database probe".
    protected abstract string Subject { get; }

    protected abstract Task<HealthCheckResult> RunAsync(HealthCheckContext context, CancellationToken ct);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await RunAsync(context, cancellationToken).WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Health check {Check} timed out", context.Registration.Name);
            return new HealthCheckResult(context.Registration.FailureStatus, $"{Subject} timed out");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check {Check} failed", context.Registration.Name);
            return new HealthCheckResult(context.Registration.FailureStatus, $"{Subject} failed");
        }
    }

    // 45 min -> "45m", 7 h -> "7h", 5.5 h -> "5.5h"
    protected static string FormatAge(TimeSpan age) =>
        age < TimeSpan.FromHours(1)
            ? $"{(int)age.TotalMinutes}m"
            : string.Create(CultureInfo.InvariantCulture, $"{age.TotalHours:0.#}h");
}
