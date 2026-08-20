using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SatelliteTracker.API.Filters;

// Beta-only admin gate (Milestone E, Step 1.2). Deliberately separate from the future
// tester AuthenticationHandler (Step 1.3) — admin access and tester identity are
// different concerns and must not share infrastructure. See CLAUDE.md.
//
// Checks the X-Admin-Key request header against the Admin:ApiKey configuration value
// (set via `dotnet user-secrets`, same mechanism as Firebase:ServiceAccountPath).
public class RequireAdminKeyAttribute : Attribute, IAsyncActionFilter
{
    private const string HeaderName = "X-Admin-Key";

    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var configuredKey = configuration["Admin:ApiKey"];

        var providedKey = context.HttpContext.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrEmpty(configuredKey) || string.IsNullOrEmpty(providedKey) || providedKey != configuredKey)
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Missing or invalid X-Admin-Key." });
            return Task.CompletedTask;
        }

        return next();
    }
}
