namespace SatelliteTracker.PassService.SGP4;

// A satellite's ground track across a window measured in orbital periods around "now" (see
// IPassService.GetOrbitTrackAsync), propagated from its latest TLE. PeriodMinutes is the TLE's
// orbital period (1440 / MeanMotion), returned so clients know how much time the window spans.
public sealed record OrbitTrack(int NoradId, double PeriodMinutes, IReadOnlyList<GroundTrackPoint> Points);
