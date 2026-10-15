using System.Security.Cryptography;
using System.Text;

namespace SatelliteTracker.Database.Security;

// Hashes beta-tester API keys for storage in ApiKey.KeyHash.
//
// Plain SHA-256 is used intentionally instead of a slow hash (bcrypt/PBKDF2/Argon2).
// Slow hashes exist to slow down brute-forcing low-entropy, human-chosen secrets
// (passwords). The raw key here is a fully random 256-bit value
// (RandomNumberGenerator.GetBytes(32)) — guessing it is computationally infeasible
// even against a fast hash, so paying the performance cost of a slow hash on every
// API request would have no security benefit.
public static class ApiKeyHasher
{
    public static string Hash(string rawKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
