namespace SatelliteTracker.PassService.SGP4;

// Computes a satellite's ground track (lat/lng/alt over time) across an arbitrary, already-known
// time window — unlike PassPredictor, which searches for AOS/LOS windows in the first place. Used
// to render the path of a specific, already-calculated Pass, not to detect passes.
public static class GroundTrackCalculator
{
    private const int DefaultStepSeconds = 10;

    // Samples the ground track at a fixed step from fromUtc to toUtc inclusive. The final point is
    // always exactly at toUtc (even if it doesn't land on a step boundary) so the track's last
    // point matches the window's end precisely.
    public static IReadOnlyList<GroundTrackPoint> ComputeGroundTrack(
        TleData tle, DateTime fromUtc, DateTime toUtc, int stepSeconds = DefaultStepSeconds)
    {
        var points = new List<GroundTrackPoint>();
        DateTime t = fromUtc;

        while (t < toUtc)
        {
            points.Add(ComputePoint(tle, t));
            t = t.AddSeconds(stepSeconds);
        }

        points.Add(ComputePoint(tle, toUtc));
        return points;
    }

    private static GroundTrackPoint ComputePoint(TleData tle, DateTime utcTime)
    {
        var pv = Sgp4Calculator.CalculatePositionVelocity(tle, utcTime);
        var geo = Sgp4Calculator.ToGeodetic(pv, utcTime);
        return new GroundTrackPoint(geo.Latitude, geo.Longitude, geo.Altitude, utcTime);
    }
}
