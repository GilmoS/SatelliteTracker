using SatelliteTracker.PassService.SGP4;
using Xunit;

namespace SatelliteTracker.Tests.PassService;

public class Sgp4CalculatorTests
{
    // ISS TLE (same verified data as TleParserTests)
    private const string IssLine1 = "1 25544U 98067A   21275.52333333  .00001234  00000-0  29279-4 0  9995";
    private const string IssLine2 = "2 25544  51.6443 213.0093 0004099  83.6831 276.4595 15.48919800303249";

    private static TleData IssTle => TleParser.Parse(IssLine1, IssLine2);

    [Fact]
    public void CalculatePositionVelocity_IssAtEpoch_PositionWithinLeoRange()
    {
        var tle = IssTle;
        var pv = Sgp4Calculator.CalculatePositionVelocity(tle, tle.Epoch);

        double r = Math.Sqrt(pv.X * pv.X + pv.Y * pv.Y + pv.Z * pv.Z);

        // ISS orbit radius: ~6750–6800 km (400 km altitude above 6378 km Earth radius)
        Assert.InRange(r, 6500.0, 7200.0);
    }

    [Fact]
    public void CalculatePositionVelocity_IssAtEpoch_VelocityWithinLeoRange()
    {
        var tle = IssTle;
        var pv = Sgp4Calculator.CalculatePositionVelocity(tle, tle.Epoch);

        double v = Math.Sqrt(pv.VX * pv.VX + pv.VY * pv.VY + pv.VZ * pv.VZ);

        // ISS orbital velocity: ~7.66 km/s
        Assert.InRange(v, 7.0, 8.5);
    }

    [Fact]
    public void CalculatePositionVelocity_OneOrbitLater_SimilarRadius()
    {
        var tle = IssTle;
        double periodMinutes = 24 * 60.0 / tle.MeanMotion;
        DateTime oneOrbitLater = tle.Epoch.AddMinutes(periodMinutes);

        var pv1 = Sgp4Calculator.CalculatePositionVelocity(tle, tle.Epoch);
        var pv2 = Sgp4Calculator.CalculatePositionVelocity(tle, oneOrbitLater);

        double r1 = Math.Sqrt(pv1.X * pv1.X + pv1.Y * pv1.Y + pv1.Z * pv1.Z);
        double r2 = Math.Sqrt(pv2.X * pv2.X + pv2.Y * pv2.Y + pv2.Z * pv2.Z);

        // Radius should be within 50 km after one orbit
        Assert.InRange(Math.Abs(r1 - r2), 0, 50.0);
    }

    [Fact]
    public void ToGeodetic_IssAtEpoch_AltitudeInLeoRange()
    {
        var tle = IssTle;
        var pv = Sgp4Calculator.CalculatePositionVelocity(tle, tle.Epoch);
        var geo = Sgp4Calculator.ToGeodetic(pv, tle.Epoch);

        // ISS altitude: ~400–420 km
        Assert.InRange(geo.Altitude, 300.0, 550.0);
    }

    [Fact]
    public void ToGeodetic_IssAtEpoch_LatitudeWithinInclinationBounds()
    {
        var tle = IssTle;
        var pv = Sgp4Calculator.CalculatePositionVelocity(tle, tle.Epoch);
        var geo = Sgp4Calculator.ToGeodetic(pv, tle.Epoch);

        // ISS inclination 51.6°, so |lat| ≤ 51.6°
        Assert.InRange(Math.Abs(geo.Latitude), 0, 52.0);
    }

    [Fact]
    public void ToGeodetic_IssAtEpoch_LongitudeInValidRange()
    {
        var tle = IssTle;
        var pv = Sgp4Calculator.CalculatePositionVelocity(tle, tle.Epoch);
        var geo = Sgp4Calculator.ToGeodetic(pv, tle.Epoch);

        Assert.InRange(geo.Longitude, -180.0, 180.0);
    }

    [Fact]
    public void CalculateObserverPosition_BenGurion_EcefRadiusCorrect()
    {
        // Ben Gurion Airport: 32.0055°N, 34.8854°E, 135m alt
        var obs = Sgp4Calculator.CalculateObserverPosition(32.0055, 34.8854, 135);
        double r = Math.Sqrt(obs.X * obs.X + obs.Y * obs.Y + obs.Z * obs.Z);

        // ECEF radius should be ~Earth radius at that lat (~6370-6380 km)
        Assert.InRange(r, 6350.0, 6400.0);
    }

    [Fact]
    public void CalculateLookAngles_IssVisible_RangePositive()
    {
        var tle = IssTle;
        var obs = Sgp4Calculator.CalculateObserverPosition(32.0055, 34.8854, 135);

        // Just verify range is always positive (geometry constraint)
        var pv = Sgp4Calculator.CalculatePositionVelocity(tle, tle.Epoch);
        var look = Sgp4Calculator.CalculateLookAngles(pv, obs, tle.Epoch);

        Assert.True(look.Range > 0);
    }

    [Fact]
    public void CalculateLookAngles_AzimuthInValidRange()
    {
        var tle = IssTle;
        var obs = Sgp4Calculator.CalculateObserverPosition(32.0055, 34.8854, 135);
        var pv = Sgp4Calculator.CalculatePositionVelocity(tle, tle.Epoch);
        var look = Sgp4Calculator.CalculateLookAngles(pv, obs, tle.Epoch);

        Assert.InRange(look.Azimuth, 0, 360.0);
    }

    [Fact]
    public void GreenwichSiderealTime_J2000Epoch_ReturnsExpectedValue()
    {
        // J2000.0 = 2000-01-01T12:00:00Z, GMST ≈ 280.46° ≈ 4.894 rad
        var j2000 = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        double gst = Sgp4Calculator.GreenwichSiderealTime(j2000);

        // GMST at J2000 is ~280.46° = 4.894 rad
        double expected = 280.46 * Math.PI / 180.0;
        Assert.InRange(gst, expected - 0.01, expected + 0.01);
    }
}
