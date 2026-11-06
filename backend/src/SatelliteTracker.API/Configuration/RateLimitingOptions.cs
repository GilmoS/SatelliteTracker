namespace SatelliteTracker.API.Configuration;

// The "RateLimiting" configuration section (defaults in appsettings.json). Every value is required
// and must be a positive integer; see RateLimitingOptionsValidator. Properties are nullable so a
// missing key fails startup instead of silently becoming 0. Used by AddAppRateLimiting.
public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    // Authenticated testers, one bucket per ApiKey.
    public TokenBucketLimit Tester { get; set; } = new();

    // Everything not authenticated (no key or an invalid key), one bucket per client IP.
    public TokenBucketLimit Anonymous { get; set; } = new();

    // POST /api/auth/register, per client IP, on top of the global limit.
    public FixedWindowLimit Register { get; set; } = new();
}

public class TokenBucketLimit
{
    // Bucket size: the largest burst allowed.
    public int? TokenLimit { get; set; }

    // Tokens added back every ReplenishmentPeriodSeconds (never above TokenLimit).
    public int? TokensPerPeriod { get; set; }

    public int? ReplenishmentPeriodSeconds { get; set; }
}

public class FixedWindowLimit
{
    public int? PermitLimit { get; set; }

    public int? WindowSeconds { get; set; }
}
