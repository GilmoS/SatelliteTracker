using Google.Apis.Auth.OAuth2;

namespace SatelliteTracker.API.Configuration;

// Turns service-account JSON content into a credential. Parsing only (including the private key):
// no token is requested and nothing goes over the network.
public static class FirebaseCredentialParser
{
    public static GoogleCredential Parse(string serviceAccountJson) =>
        GoogleCredential.FromServiceAccountCredential(
            CredentialFactory.FromJson<ServiceAccountCredential>(serviceAccountJson));
}
