using Microsoft.Extensions.Options;
using Npgsql;

namespace SatelliteTracker.API.Configuration;

public class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    private const string Key = "ConnectionStrings:DefaultConnection";

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

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
