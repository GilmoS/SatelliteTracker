using Microsoft.AspNetCore.Authentication;

namespace SatelliteTracker.API.Authentication;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiKey";

    public string HeaderName { get; set; } = "X-Api-Key";
}
