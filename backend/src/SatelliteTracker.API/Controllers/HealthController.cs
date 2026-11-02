using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.API.Filters;

namespace SatelliteTracker.API.Controllers;

// Per-check breakdown for a person looking into a problem. A controller action rather than a mapped
// health endpoint because RequireAdminKeyAttribute is an MVC filter, and this must use that same
// mechanism. /health/live and /health/ready are not here: see HealthChecksExtensions.
[ApiController]
[Route("health")]
[RequireAdminKey]
[ApiExplorerSettings(IgnoreApi = true)]
public class HealthController : BaseController
{
    // Used if a check somehow throws past BoundedHealthCheck: the framework then puts the exception
    // message in the entry's Description, which must not reach a response.
    private const string UnexpectedFailureReason = "check failed";

    private readonly HealthCheckService _healthChecks;

    public HealthController(HealthCheckService healthChecks) => _healthChecks = healthChecks;

    // GET health/details
    [HttpGet("details")]
    public async Task<IActionResult> GetDetails()
    {
        var report = await _healthChecks.CheckHealthAsync(HttpContext.RequestAborted);

        var checks = report.Entries
            .Select(entry => new HealthCheckDetailDto(
                entry.Key,
                entry.Value.Status.ToString(),
                Math.Round(entry.Value.Duration.TotalMilliseconds, 1),
                entry.Value.Exception is null ? entry.Value.Description ?? string.Empty : UnexpectedFailureReason))
            .ToList();

        Response.Headers.CacheControl = "no-store";

        return StatusCode(
            report.Status == HealthStatus.Unhealthy ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status200OK,
            new HealthDetailsDto(report.Status.ToString(), checks));
    }
}
