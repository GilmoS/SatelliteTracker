namespace SatelliteTracker.PassService.SGP4;

// this record represents the position and velocity of a satellite in Earth-centered inertial (ECI) coordinates
public sealed record PositionVelocity(double X, double Y, double Z,double VX, double VY, double VZ);

