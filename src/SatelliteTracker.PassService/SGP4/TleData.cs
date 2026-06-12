namespace SatelliteTracker.PassService.SGP4;

public sealed class TleData
{
    public int SatelliteNumber { get; init; }
    public char Classification { get; init; }
    public string InternationalDesignator { get; init; } = string.Empty;
    public DateTime Epoch { get; init; }
    public double MeanMotionDot { get; init; }
    public double MeanMotionDotDot { get; init; }
    public double BStarDrag { get; init; }
    public double Inclination { get; init; }
    public double RightAscension { get; init; }
    public double Eccentricity { get; init; }
    public double ArgumentOfPerigee { get; init; }
    public double MeanAnomaly { get; init; }
    public double MeanMotion { get; init; }
    public int RevolutionNumber { get; init; }
}
