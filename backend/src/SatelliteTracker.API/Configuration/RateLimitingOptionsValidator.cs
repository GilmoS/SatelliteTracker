using Microsoft.Extensions.Options;

namespace SatelliteTracker.API.Configuration;

// Every RateLimiting value must be present and a positive integer. All failures are reported
// together, each naming the full key. A value that isn't an integer at all is rejected earlier by
// the configuration binder, which also names the key.
public class RateLimitingOptionsValidator : IValidateOptions<RateLimitingOptions>
{
    private const string Section = RateLimitingOptions.SectionName;

    public ValidateOptionsResult Validate(string? name, RateLimitingOptions options)
    {
        var failures = new List<string>();

        CheckTokenBucket(failures, $"{Section}:Tester", options.Tester);
        CheckTokenBucket(failures, $"{Section}:Anonymous", options.Anonymous);

        var register = options.Register ?? new FixedWindowLimit();
        CheckPositive(failures, $"{Section}:Register:PermitLimit", register.PermitLimit);
        CheckPositive(failures, $"{Section}:Register:WindowSeconds", register.WindowSeconds);

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void CheckTokenBucket(List<string> failures, string prefix, TokenBucketLimit? limit)
    {
        limit ??= new TokenBucketLimit();
        CheckPositive(failures, $"{prefix}:TokenLimit", limit.TokenLimit);
        CheckPositive(failures, $"{prefix}:TokensPerPeriod", limit.TokensPerPeriod);
        CheckPositive(failures, $"{prefix}:ReplenishmentPeriodSeconds", limit.ReplenishmentPeriodSeconds);
    }

    private static void CheckPositive(List<string> failures, string key, int? value)
    {
        if (value is null) failures.Add($"{key} is required.");
        else if (value <= 0) failures.Add($"{key} must be a positive integer.");
    }
}
