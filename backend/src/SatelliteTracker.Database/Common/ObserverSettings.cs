using System.ComponentModel.DataAnnotations;

namespace SatelliteTracker.Database.Common;

// The "Observer" configuration section (the ground station passes are computed for).
// Nullable + [Required] so a missing value fails startup validation instead of silently
// defaulting to 0 (lat/lng 0,0). Consumers can read .Value: the host does not start otherwise.
public class ObserverSettings
{
    public const string SectionName = "Observer";

    [Required(ErrorMessage = "Observer:Lat is required.")]
    [Range(-90.0, 90.0, ErrorMessage = "Observer:Lat must be between -90 and 90.")]
    public double? Lat { get; init; }

    [Required(ErrorMessage = "Observer:Lng is required.")]
    [Range(-180.0, 180.0, ErrorMessage = "Observer:Lng must be between -180 and 180.")]
    public double? Lng { get; init; }

    [Required(ErrorMessage = "Observer:AltMeters is required.")]
    [Range(-500.0, 9000.0, ErrorMessage = "Observer:AltMeters must be between -500 and 9000.")]
    public double? AltMeters { get; init; }

    [Required(ErrorMessage = "Observer:MinElevationDeg is required.")]
    [Range(0.0, 90.0, ErrorMessage = "Observer:MinElevationDeg must be between 0 and 90.")]
    public double? MinElevationDeg { get; init; }
}
