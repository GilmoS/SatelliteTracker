namespace SatelliteTracker.API.Authentication;

// Custom claim types used by ApiKeyAuthenticationHandler. Kept out of System.Security.Claims'
// ClaimTypes since ApiKeyId/DisplayName have no standard equivalent there.
internal static class ApiKeyClaimTypes
{
    public const string ApiKeyId = "api_key_id";
    public const string DisplayName = "display_name";
}
