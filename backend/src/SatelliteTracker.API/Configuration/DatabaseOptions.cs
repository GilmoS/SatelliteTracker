using System.ComponentModel.DataAnnotations;

namespace SatelliteTracker.API.Configuration;

// ConnectionStrings:DefaultConnection, bound as options so it is validated at startup with the
// rest of the configuration. DatabaseOptionsValidator checks its shape; it never connects.
public class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";

    [Required(ErrorMessage = "ConnectionStrings:DefaultConnection is required and must not be empty.")]
    public string? DefaultConnection { get; set; }
}
