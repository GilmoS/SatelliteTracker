namespace SatelliteTracker.PassService.SGP4;
// This record represents the look angles (azimuth, elevation, and range) from an observer to a satellite.
public sealed record LookAngles(double Azimuth, double Elevation, double Range);
