using System.ComponentModel.DataAnnotations;

namespace SatelliteTracker.API.Configuration;

// The "Admin" configuration section: the X-Admin-Key value checked by RequireAdminKeyAttribute.
public class AdminOptions
{
    public const string SectionName = "Admin";

    [Required(ErrorMessage = "Admin:ApiKey is required and must not be empty.")]
    [MinLength(32, ErrorMessage = "Admin:ApiKey must be at least 32 characters long.")]
    public string? ApiKey { get; set; }
}
