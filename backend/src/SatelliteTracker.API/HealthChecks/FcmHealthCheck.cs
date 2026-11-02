using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SatelliteTracker.API.HealthChecks;

// Can we get an access token from the Firebase credential? No message is sent.
public sealed class FcmHealthCheck : BoundedHealthCheck
{
    private readonly IFcmTokenProbe _probe;

    public FcmHealthCheck(IFcmTokenProbe probe, ILogger<FcmHealthCheck> logger) : base(logger)
    {
        _probe = probe;
    }

    protected override string Subject => "FCM token request";

    protected override async Task<HealthCheckResult> RunAsync(HealthCheckContext context, CancellationToken ct)
    {
        await _probe.RequestTokenAsync(ct);
        return HealthCheckResult.Healthy("access token available");
    }
}
