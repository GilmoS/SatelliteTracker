namespace SatelliteTracker.PassService.SGP4;

public sealed record PositionVelocity(
    double X, double Y, double Z,
    double VX, double VY, double VZ);
