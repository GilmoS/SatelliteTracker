using System.Security.Cryptography;
using System.Text.Json;

namespace SatelliteTracker.Tests.API.Infrastructure;

// Builds a syntactically valid Firebase service-account JSON in memory, around an RSA key
// generated at test runtime. It parses into a credential (which is all startup validation checks)
// but belongs to no real project. Never replace this with a committed key file, fake or not:
// secret scanners flag any private key in the repo.
public static class TestServiceAccount
{
    public static string CreateJson()
    {
        using var rsa = RSA.Create(2048);

        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "service_account",
            ["project_id"] = "sattrakk-test",
            ["private_key_id"] = "test-key-id",
            ["private_key"] = rsa.ExportPkcs8PrivateKeyPem(),
            ["client_email"] = "test@sattrakk-test.iam.gserviceaccount.com",
            ["client_id"] = "000000000000000000000",
            ["token_uri"] = "https://oauth2.googleapis.com/token"
        });
    }
}
