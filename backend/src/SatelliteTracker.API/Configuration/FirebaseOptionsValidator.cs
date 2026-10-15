using Microsoft.Extensions.Options;

namespace SatelliteTracker.API.Configuration;

public class FirebaseOptionsValidator : IValidateOptions<FirebaseOptions>
{
    public ValidateOptionsResult Validate(string? name, FirebaseOptions options)
    {
        // A missing value is reported by the [Required] check.
        if (string.IsNullOrWhiteSpace(options.ServiceAccountJson))
            return ValidateOptionsResult.Skip;

        try
        {
            FirebaseCredentialParser.Parse(options.ServiceAccountJson);
            return ValidateOptionsResult.Success;
        }
        catch (Exception ex)
        {
            // Only the exception type: the message of a parse error can quote the JSON.
            return ValidateOptionsResult.Fail(
                "Firebase:ServiceAccountJson is not a valid service-account credential JSON " +
                $"(expected \"type\": \"service_account\" with client_email and private_key; {ex.GetType().Name}).");
        }
    }
}
