using SatelliteTracker.PassService.SGP4;
using Xunit;

namespace SatelliteTracker.Tests.PassService;

public class PassPredictorTests
{
    private const string IssLine1 = "1 25544U 98067A   21275.52333333  .00001234  00000-0  29279-4 0  9995";
    private const string IssLine2 = "2 25544  51.6443 213.0093 0004099  83.6831 276.4595 15.48919800303249";

    // Ben Gurion Airport
    private const double ObserverLat = 32.0055;
    private const double ObserverLng = 34.8854;
    private const double ObserverAltM = 135;
    private const double MinElevation = 5.0;

    private static TleData IssTle => TleParser.Parse(IssLine1, IssLine2);

    private static IReadOnlyList<PassResult> GetPasses(int days = 7)
    {
        var tle = IssTle;
        var from = tle.Epoch;
        var to = from.AddDays(days);
        var satelliteId = Guid.NewGuid();
        return PassPredictor.PredictPasses(tle, satelliteId, ObserverLat, ObserverLng, ObserverAltM, from, to, MinElevation).ToList();
    }

    [Fact]
    public void PredictPasses_SevenDays_AtLeastOnePassFound()
    {
        var passes = GetPasses(7);
        Assert.NotEmpty(passes);
    }

    [Fact]
    public void PredictPasses_AllPasses_AosBeforeLos()
    {
        var passes = GetPasses(7);
        Assert.All(passes, p => Assert.True(p.AOS < p.LOS));
    }

    [Fact]
    public void PredictPasses_AllPasses_MaxElevationAboveMinimum()
    {
        var passes = GetPasses(7);
        Assert.All(passes, p => Assert.True(p.MaxElevation >= MinElevation));
    }

    [Fact]
    public void PredictPasses_AllPasses_DurationPositive()
    {
        var passes = GetPasses(7);
        Assert.All(passes, p => Assert.True(p.DurationSeconds > 0));
    }

    [Fact]
    public void PredictPasses_AllPasses_DurationMatchesAosLos()
    {
        var passes = GetPasses(7);
        Assert.All(passes, p =>
        {
            int expected = (int)(p.LOS - p.AOS).TotalSeconds;
            Assert.Equal(expected, p.DurationSeconds);
        });
    }

    [Fact]
    public void PredictPasses_AllPasses_AosAzimuthInRange()
    {
        var passes = GetPasses(7);
        Assert.All(passes, p => Assert.InRange(p.AosAzimuth, 0, 360.0));
    }

    [Fact]
    public void PredictPasses_AllPasses_LosAzimuthInRange()
    {
        var passes = GetPasses(7);
        Assert.All(passes, p => Assert.InRange(p.LosAzimuth, 0, 360.0));
    }

    [Fact]
    public void PredictPasses_AllPasses_WithinRequestedTimeWindow()
    {
        var tle = IssTle;
        var from = tle.Epoch;
        var to = from.AddDays(7);
        var passes = PassPredictor.PredictPasses(tle, Guid.NewGuid(), ObserverLat, ObserverLng, ObserverAltM, from, to).ToList();

        Assert.All(passes, p =>
        {
            Assert.True(p.AOS >= from.AddMinutes(-2)); // allow pass already in progress at window start
            Assert.True(p.LOS <= to.AddMinutes(10));   // allow slight overshoot at window end
        });
    }

    [Fact]
    public void PredictPasses_AllPasses_SatelliteIdPreserved()
    {
        var tle = IssTle;
        var id = Guid.NewGuid();
        var passes = PassPredictor.PredictPasses(tle, id, ObserverLat, ObserverLng, ObserverAltM,
            tle.Epoch, tle.Epoch.AddDays(7)).ToList();

        Assert.All(passes, p => Assert.Equal(id, p.SatelliteId));
    }

    [Fact]
    public void PredictPasses_IssOverOneDay_ReasonablePassCount()
    {
        // ISS does ~15.5 orbits/day; from a given location, up to ~10 may be visible
        var passes = GetPasses(1);
        Assert.InRange(passes.Count, 0, 15);
    }

    [Fact]
    public void PredictPasses_MaxElevationLessThan90()
    {
        var passes = GetPasses(7);
        Assert.All(passes, p => Assert.True(p.MaxElevation <= 90.0));
    }
}
