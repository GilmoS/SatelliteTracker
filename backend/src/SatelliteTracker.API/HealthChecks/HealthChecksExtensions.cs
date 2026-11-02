using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SatelliteTracker.API.HealthChecks;

public static class HealthChecksExtensions
{
    public static IServiceCollection AddAppHealthChecks(this IServiceCollection services)
    {
        services.AddSingleton<IHealthDatabaseProbe, NpgsqlHealthDatabaseProbe>();
        services.AddSingleton<IFcmTokenProbe, FirebaseFcmTokenProbe>();

        string[] ready = [HealthCheckPolicy.ReadyTag];
        var timeout = HealthCheckPolicy.CheckTimeout;

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", HealthStatus.Unhealthy, ready, timeout)
            .AddCheck<TleFreshnessHealthCheck>("tle-freshness", HealthStatus.Degraded, ready, timeout)
            .AddCheck<PassFreshnessHealthCheck>("pass-freshness", HealthStatus.Degraded, ready, timeout)
            .AddCheck<FcmHealthCheck>("fcm", HealthStatus.Degraded, ready, timeout);

        return services;
    }

    // /health/live and /health/ready, anonymous. Call this before UseAuthentication: the ApiKey
    // handler looks a sent X-Api-Key up in the database (through the retry strategy), and a probe
    // must never wait on that. /health/details is HealthController, behind the admin key.
    public static WebApplication UseHealthProbes(this WebApplication app)
    {
        // Runs no checks: Healthy means the process can answer.
        app.UseHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteStatusWord
        });

        // The framework's default status codes are the contract: 200 / 200 / 503.
        app.UseHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(HealthCheckPolicy.ReadyTag),
            ResponseWriter = WriteStatusWord
        });

        return app;
    }

    // These two endpoints are public, so the body is one word and nothing about why.
    private static Task WriteStatusWord(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "text/plain";
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsync(report.Status.ToString());
    }
}
