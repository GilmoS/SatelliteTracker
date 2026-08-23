namespace SatelliteTracker.PassService.SGP4;

// A single point along a satellite's ground track, computed via SGP4 propagation at a specific instant.
public sealed record GroundTrackPoint(double Latitude, double Longitude, double Altitude, DateTime TimestampUtc);
