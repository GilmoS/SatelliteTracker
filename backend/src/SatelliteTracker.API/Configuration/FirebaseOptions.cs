using System.ComponentModel.DataAnnotations;

namespace SatelliteTracker.API.Configuration;

// The "Firebase" configuration section. ServiceAccountJson holds the service-account key file's
// JSON content (not a path), so it can come from user-secrets, an environment variable or, later,
// Key Vault. FirebaseOptionsValidator checks that it parses into a service-account credential.
public class FirebaseOptions
{
    public const string SectionName = "Firebase";

    [Required(ErrorMessage = "Firebase:ServiceAccountJson is required and must not be empty.")]
    public string? ServiceAccountJson { get; set; }
}
