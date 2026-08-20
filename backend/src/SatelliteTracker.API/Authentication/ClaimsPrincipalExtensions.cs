using System.Security.Claims;

namespace SatelliteTracker.API.Authentication;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Reads the authenticated tester's ApiKeyId from the claims set by
    /// ApiKeyAuthenticationHandler. Only valid to call on an [Authorize]-protected action using
    /// the "ApiKey" scheme — throws if the principal has no such claim.
    /// </summary>
    public static Guid GetApiKeyId(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(ApiKeyClaimTypes.ApiKeyId)
            ?? throw new InvalidOperationException(
                "ClaimsPrincipal has no api_key_id claim — is this action [Authorize]d with the ApiKey scheme?");

        return Guid.Parse(claim.Value);
    }
}
