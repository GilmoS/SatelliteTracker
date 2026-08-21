using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.Database.Security;

namespace SatelliteTracker.API.Authentication;

// Tester authentication scheme (Milestone E, Step 1.3). Reads the X-Api-Key header, hashes it
// with the existing ApiKeyHasher, and looks up the matching ApiKey row. Registered as the
// default scheme in Program.cs so plain [Authorize] works on controllers/actions.
//
// Uniform-failure rule: a missing header, a well-formed-but-unknown key, and a
// found-but-inactive key must all produce the exact same 401 response. Distinguishing them in
// the response would let a caller enumerate which keys exist/are active, so HandleAuthenticateAsync
// returns a single generic Fail(...) for every non-success case, and HandleChallengeAsync writes
// one fixed JSON body regardless of the underlying reason.
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IApiKeyRepository _apiKeyRepository;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyRepository apiKeyRepository)
        : base(options, logger, encoder)
    {
        _apiKeyRepository = apiKeyRepository;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var headerValues))
            return AuthenticateResult.NoResult();

        var rawKey = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(rawKey))
            return AuthenticateResult.NoResult();

        var keyHash = ApiKeyHasher.Hash(rawKey);
        var lookupResult = await _apiKeyRepository.GetByHashAsync(keyHash);

        if (!lookupResult.IsSuccess || !lookupResult.Value!.IsActive)
            return AuthenticateResult.Fail("Invalid API key.");

        var apiKey = lookupResult.Value!;
        await _apiKeyRepository.UpdateLastUsedAtAsync(apiKey.Id, DateTimeOffset.UtcNow);

        var claims = new[]
        {
            new Claim(ApiKeyClaimTypes.ApiKeyId, apiKey.Id.ToString()),
            new Claim(ClaimTypes.Email, apiKey.Email),
            new Claim(ApiKeyClaimTypes.DisplayName, apiKey.DisplayName)
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";
        return Response.WriteAsync("""{"error":"Missing or invalid API key."}""");
    }
}
