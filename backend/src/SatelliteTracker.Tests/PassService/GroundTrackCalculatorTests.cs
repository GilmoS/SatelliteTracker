using SatelliteTracker.PassService.SGP4;
using Xunit;

namespace SatelliteTracker.Tests.PassService;

public class GroundTrackCalculatorTests
{
    // ISS TLE (same verified data as PassPredictorTests/Sgp4CalculatorTests)
    private const string IssLine1 = "1 25544U 98067A   21275.52333333  .00001234  00000-0  29279-4 0  9995";
    private const string IssLine2 = "2 25544  51.6443 213.0093 0004099  83.6831 276.4595 15.48919800303249";

    private static TleData IssTle => TleParser.Parse(IssLine1, IssLine2);

    [Fact]
    public void ComputeGroundTrack_ReturnsNonEmptyTrack()
    {
        var tle = IssTle;
        var points = GroundTrackCalculator.ComputeGroundTrack(tle, tle.Epoch, tle.Epoch.AddMinutes(10));

        Assert.NotEmpty(points);
    }

    [Fact]
    public void ComputeGroundTrack_FirstAndLastPoints_MatchWindowBounds()
    {
        var tle = IssTle;
        var from = tle.Epoch;
        var to = tle.Epoch.AddMinutes(10);
        var points = GroundTrackCalculator.ComputeGroundTrack(tle, from, to);

        Assert.Equal(from, points[0].TimestampUtc);
        Assert.Equal(to, points[^1].TimestampUtc);
    }

    [Fact]
    public void ComputeGroundTrack_AllPoints_WithinRequestedWindow()
    {
        var tle = IssTle;
        var from = tle.Epoch;
        var to = tle.Epoch.AddMinutes(10);
        var points = GroundTrackCalculator.ComputeGroundTrack(tle, from, to);

        Assert.All(points, p => Assert.InRange(p.TimestampUtc, from, to));
    }

    [Fact]
    public void ComputeGroundTrack_AllLatitudes_WithinInclinationBounds()
    {
        var tle = IssTle;
        var points = GroundTrackCalculator.ComputeGroundTrack(tle, tle.Epoch, tle.Epoch.AddMinutes(10));

        // ISS inclination 51.6°, so |lat| should never exceed it
        Assert.All(points, p => Assert.InRange(Math.Abs(p.Latitude), 0, 52.0));
    }

    [Fact]
    public void ComputeGroundTrack_SameInputs_ProducesIdenticalOutput()
    {
        var tle = IssTle;
        var from = tle.Epoch;
        var to = tle.Epoch.AddMinutes(10);

        var first = GroundTrackCalculator.ComputeGroundTrack(tle, from, to);
        var second = GroundTrackCalculator.ComputeGroundTrack(tle, from, to);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ComputeGroundTrack_ShorterStep_ProducesMorePoints()
    {
        var tle = IssTle;
        var from = tle.Epoch;
        var to = tle.Epoch.AddMinutes(10);

        var coarse = GroundTrackCalculator.ComputeGroundTrack(tle, from, to, stepSeconds: 60);
        var fine = GroundTrackCalculator.ComputeGroundTrack(tle, from, to, stepSeconds: 5);

        Assert.True(fine.Count > coarse.Count);
    }
}
