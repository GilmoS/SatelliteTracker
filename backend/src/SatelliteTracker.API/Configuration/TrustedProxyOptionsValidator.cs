using Microsoft.Extensions.Options;

namespace SatelliteTracker.API.Configuration;

// Every ForwardedHeaders:KnownProxies entry must be a single IP address. Failures name the entry
// by index, not by value, like the other startup validators.
public class TrustedProxyOptionsValidator : IValidateOptions<TrustedProxyOptions>
{
    public ValidateOptionsResult Validate(string? name, TrustedProxyOptions options)
    {
        var failures = new List<string>();
        for (var i = 0; i < options.KnownProxies.Count; i++)
        {
            if (!TrustedProxyOptions.TryParseProxy(options.KnownProxies[i], out _))
                failures.Add($"{TrustedProxyOptions.SectionName}:KnownProxies:{i} must be a single IP address (no range, host name or port).");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
