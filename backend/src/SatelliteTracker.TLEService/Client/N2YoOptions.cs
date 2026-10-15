using System.ComponentModel.DataAnnotations;

namespace SatelliteTracker.TLEService.Client;

// The "N2YO" configuration section. Bound and validated at startup by the API
// (AddValidatedConfiguration), so a missing key stops the app instead of failing on the first call.
public class N2YoOptions
{
    public const string SectionName = "N2YO";

    [Required(ErrorMessage = "N2YO:ApiKey is required and must not be empty.")]
    public string? ApiKey { get; set; }
}
