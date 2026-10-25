using Microsoft.Extensions.Options;
using Npgsql;

namespace SatelliteTracker.API.Configuration;

public class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    private const string Key = "ConnectionStrings:DefaultConnection";
    private const string AzureHostSuffix = ".postgres.database.azure.com";

    // Host may list several servers (comma-separated, each optionally with :port); any Azure one
    // triggers the rule.
    private static bool IsAzureHost(string? host) =>
        !string.IsNullOrWhiteSpace(host) && host
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(h => h.Split(':')[0].TrimEnd('.'))
            .Any(h => h.EndsWith(AzureHostSuffix, StringComparison.OrdinalIgnoreCase));

    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        // A missing value is reported by the [Required] check.
        if (string.IsNullOrWhiteSpace(options.DefaultConnection))
            return ValidateOptionsResult.Skip;

        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(options.DefaultConnection);
        }
        catch (Exception ex)
        {
            // Only the exception type: the parser's message can quote part of the string.
            return ValidateOptionsResult.Fail($"{Key} is not a valid PostgreSQL connection string ({ex.GetType().Name}).");
        }

        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(builder.Host)) failures.Add($"{Key} must specify Host.");
        if (string.IsNullOrWhiteSpace(builder.Database)) failures.Add($"{Key} must specify Database.");
        if (string.IsNullOrWhiteSpace(builder.Username)) failures.Add($"{Key} must specify Username.");

        // Azure Database for PostgreSQL requires TLS; VerifyFull also checks the certificate chain
        // and that it was issued for this host. Other hosts (localhost, a compose service name)
        // have no SSL requirement.
        if (IsAzureHost(builder.Host) && builder.SslMode != SslMode.VerifyFull)
            failures.Add($"{Key} must set SSL Mode=VerifyFull when Host is an Azure Database for PostgreSQL server ({AzureHostSuffix}).");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
