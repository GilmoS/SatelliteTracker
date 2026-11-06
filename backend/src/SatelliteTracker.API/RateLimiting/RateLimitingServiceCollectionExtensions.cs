using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SatelliteTracker.API.Authentication;
using SatelliteTracker.API.Configuration;

namespace SatelliteTracker.API.RateLimiting;

public static class RateLimitPolicies
{
    // Endpoint policy for POST /api/auth/register, applied with [EnableRateLimiting].
    public const string Register = "register";
}

// Built-in ASP.NET Core rate limiting, in memory (single instance by design). Limits come from
// RateLimitingOptions, validated at startup. See "Proxy & rate limiting" in CLAUDE.md.
//
// Global limiter, one partition per request:
//   - /health/live and /health/ready: no limit.
//   - An authenticated tester: a token bucket keyed by the ApiKeyId claim that
//     ApiKeyAuthenticationHandler set. Never the raw X-Api-Key header: a random or invalid key
//     must not get a bucket of its own, or rotating fake keys would bypass every limit.
//   - Everything else (no key, unknown key, inactive key): a token bucket keyed by client IP,
//     i.e. RemoteIpAddress after UseForwardedHeaders.
// The register policy is a fixed window per client IP, enforced on top of the global limiter.
//
// Middleware order this depends on: UseForwardedHeaders (client IP) → UseAuthentication
// (tester identity) → UseRateLimiter → UseAuthorization.
public static class RateLimitingServiceCollectionExtensions
{
    private const string RejectionBody = """{"error":"rate_limited"}""";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingOptions>>((options, settings) => Configure(options, settings.Value));

        return services;
    }

    private static void Configure(RateLimiterOptions options, RateLimitingOptions settings)
    {
        var tester = ToTokenBucket(settings.Tester);
        var anonymous = ToTokenBucket(settings.Anonymous);
        var register = new FixedWindowRateLimiterOptions
        {
            PermitLimit = settings.Register.PermitLimit!.Value,
            Window = TimeSpan.FromSeconds(settings.Register.WindowSeconds!.Value),
            QueueLimit = 0,
            AutoReplenishment = true
        };

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = WriteRejectionAsync;

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
            context => GlobalPartition(context, tester, anonymous));

        options.AddPolicy(RateLimitPolicies.Register, context =>
            RateLimitPartition.GetFixedWindowLimiter($"register:{ClientIpKey(context)}", _ => register));
    }

    internal static RateLimitPartition<string> GlobalPartition(
        HttpContext context, TokenBucketRateLimiterOptions tester, TokenBucketRateLimiterOptions anonymous)
    {
        if (IsExemptHealthProbe(context.Request.Path))
            return RateLimitPartition.GetNoLimiter("exempt");

        var apiKeyId = AuthenticatedApiKeyId(context);
        if (apiKeyId is not null)
            return RateLimitPartition.GetTokenBucketLimiter($"tester:{apiKeyId}", _ => tester);

        return RateLimitPartition.GetTokenBucketLimiter($"ip:{ClientIpKey(context)}", _ => anonymous);
    }

    // The ApiKeyId only from an identity our ApiKey scheme authenticated. A request whose key
    // failed authentication has no such identity and so falls into its IP's partition.
    private static string? AuthenticatedApiKeyId(HttpContext context) =>
        context.User.Identities
            .FirstOrDefault(i => i.IsAuthenticated && i.AuthenticationType == ApiKeyAuthenticationOptions.SchemeName)
            ?.FindFirst(ApiKeyClaimTypes.ApiKeyId)?.Value;

    // The two unauthenticated probes for Docker and the uptime monitor. Their middleware already
    // answers before UseRateLimiter; this keeps them exempt if the pipeline is ever reordered.
    // /health/details is deliberately not exempt.
    private static bool IsExemptHealthProbe(PathString path) =>
        IsPath(path, "/health/live") || IsPath(path, "/health/ready");

    private static bool IsPath(PathString path, string probe) =>
        path.StartsWithSegments(probe, StringComparison.OrdinalIgnoreCase, out var rest)
        && (!rest.HasValue || rest.Value == "/");

    // RemoteIpAddress after UseForwardedHeaders: Caddy's view of the client when the request came
    // through the trusted proxy, the TCP peer otherwise. IPv4-mapped IPv6 is folded to IPv4 so one
    // client can't be counted under two keys.
    internal static string ClientIpKey(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null) return "unknown";
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        return address.ToString();
    }

    private static TokenBucketRateLimiterOptions ToTokenBucket(TokenBucketLimit limit) => new()
    {
        TokenLimit = limit.TokenLimit!.Value,
        TokensPerPeriod = limit.TokensPerPeriod!.Value,
        ReplenishmentPeriod = TimeSpan.FromSeconds(limit.ReplenishmentPeriodSeconds!.Value),
        QueueLimit = 0,
        AutoReplenishment = true
    };

    // 429 (set by the middleware from RejectionStatusCode), Retry-After in whole seconds when the
    // limiter knows it, and a fixed minimal body.
    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken ct)
    {
        var response = context.HttpContext.Response;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }

        response.ContentType = "application/json";
        await response.WriteAsync(RejectionBody, ct);
    }
}
