using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;
using SatelliteTracker.API.Configuration;

namespace SatelliteTracker.API.HealthChecks;

// Registered as a singleton so the credential keeps its token between checks and Google's token
// endpoint is contacted only when the token needs refreshing (about hourly), not on every probe.
// So a key revoked or an outage started while a token is still valid shows up at the next refresh,
// which is also when real FCM sends would start failing.
public sealed class FirebaseFcmTokenProbe : IFcmTokenProbe
{
    private const string MessagingScope = "https://www.googleapis.com/auth/firebase.messaging";

    private readonly ITokenAccess _credential;

    public FirebaseFcmTokenProbe(IOptions<FirebaseOptions> options)
    {
        // A scope, and no authUri below, is what makes this a real token request to Google. Given an
        // authUri instead, a service-account credential signs its own JWT locally without contacting
        // anyone, so this check could not fail for a revoked key or an outage.
        _credential = FirebaseCredentialParser.Parse(options.Value.ServiceAccountJson!)
            .CreateScoped(MessagingScope);
    }

    public async Task RequestTokenAsync(CancellationToken ct)
    {
        var token = await _credential.GetAccessTokenForRequestAsync(authUri: null, ct);

        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException("The Firebase credential returned an empty access token.");
    }
}
