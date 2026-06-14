namespace SatelliteTracker.PassService.SGP4;

// This record represents a geographic position on Earth, defined by latitude, longitude, and altitude.
public sealed record GeoPosition(double Latitude, double Longitude, double Altitude);
