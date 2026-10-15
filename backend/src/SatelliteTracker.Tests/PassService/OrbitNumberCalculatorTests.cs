using SatelliteTracker.PassService.SGP4;
using Xunit;

namespace SatelliteTracker.Tests.PassService;

public class OrbitNumberCalculatorTests
{
    // Real consecutive TLE sets for EROS C3 (NORAD 54880) and RUNNER-1 (NORAD 56953), taken from the
    // dev database's TLE history. Like every stored set, each has its epoch exactly on an ascending
    // node. Both pairs are cases where the older TLE puts that node a fraction of a second AFTER
    // the newer epoch — the boundary case the epoch-node tolerance exists for.
    private static readonly TleData ErosA = TleParser.Parse(
        "1 54880U 22179A   26249.24744956  .00001720  00000-0  86457-4 0  9994",
        "2 54880 139.3352 108.0530 0007900 188.8389 171.2280 15.11364290204594"); // rev 20459
    private static readonly TleData ErosB = TleParser.Parse(
        "1 54880U 22179A   26253.27827296  .00002487  00000-0  13073-3 0  9995",
        "2 54880 139.3347 131.0916 0008568 215.9148 144.1084 15.11381575205203"); // rev 20520

    private static readonly TleData RunnerA = TleParser.Parse(
        "1 56953U 23084X   26232.14069745  .00009941  00000-0  26083-3 0  9991",
        "2 56953  97.6319  16.7530 0009143  87.2318 272.9973 15.38823230177439"); // rev 17743
    private static readonly TleData RunnerB = TleParser.Parse(
        "1 56953U 23084X   26232.92101371  .00010121  00000-0  26535-3 0  9993",
        "2 56953  97.6318  17.5651 0009135  84.8279 275.4008 15.38839818177550"); // rev 17755

    // Same observer as ObserverSettings in appsettings.json.
    private const double ObserverLat = 32.0055;
    private const double ObserverLng = 34.8854;
    private const double ObserverAltM = 135.0;

    public static TheoryData<string> Pairs => new() { "EROS C3", "RUNNER-1" };

    private static (TleData Older, TleData Newer) Pair(string name)
        => name == "EROS C3" ? (ErosA, ErosB) : (RunnerA, RunnerB);

    private static double HalfPeriodMinutes(TleData tle) => 720.0 / tle.MeanMotion;

    private static List<PassResult> PredictWeek(TleData tle, DateTime from)
        => PassPredictor.PredictPasses(tle, Guid.Empty, ObserverLat, ObserverLng, ObserverAltM,
            from, from.AddDays(7)).ToList();

    // ── Consistency ──────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Pairs))]
    public void OlderTle_PropagatedHalfAnOrbitPastNewerEpoch_MatchesNewerTlesRevolutionNumber(string name)
    {
        var (older, newer) = Pair(name);

        // Compared half an orbit after the newer epoch, not at it: both epochs sit on an ascending
        // node, so "has the node at this instant been crossed yet" is ambiguous at the epoch itself.
        var t = newer.Epoch.AddMinutes(HalfPeriodMinutes(newer));

        Assert.Equal(newer.RevolutionNumber, OrbitNumberCalculator.ComputeRevolutionNumber(older, t));
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void TleOwnEpochNode_IsNotCountedAgain(string name)
    {
        var (_, tle) = Pair(name);

        Assert.Equal(tle.RevolutionNumber, OrbitNumberCalculator.ComputeRevolutionNumber(tle, tle.Epoch));
        Assert.Equal(tle.RevolutionNumber,
            OrbitNumberCalculator.ComputeRevolutionNumber(tle, tle.Epoch.AddMinutes(HalfPeriodMinutes(tle))));
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void OneOrbitLater_RevolutionNumberIncreasesByOne_AndHalfAnOrbitEarlier_DecreasesByOne(string name)
    {
        var (_, tle) = Pair(name);
        var half = HalfPeriodMinutes(tle);

        Assert.Equal(tle.RevolutionNumber + 1,
            OrbitNumberCalculator.ComputeRevolutionNumber(tle, tle.Epoch.AddMinutes(3 * half)));
        Assert.Equal(tle.RevolutionNumber - 1,
            OrbitNumberCalculator.ComputeRevolutionNumber(tle, tle.Epoch.AddMinutes(-half)));
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void BatchResults_MatchSingleResults_InInputOrder(string name)
    {
        var (_, tle) = Pair(name);
        var times = new List<DateTime>
        {
            tle.Epoch.AddDays(5), tle.Epoch.AddHours(-3), tle.Epoch.AddDays(1), tle.Epoch.AddMinutes(30)
        };

        var batch = OrbitNumberCalculator.ComputeRevolutionNumbers(tle, times);

        Assert.Equal(times.Select(t => OrbitNumberCalculator.ComputeRevolutionNumber(tle, t)), batch);
    }

    // ── Uniqueness ───────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Pairs))]
    public void ConsecutivePasses_LessThanAnOrbitApart_GetDifferentOrbitNumbers(string name)
    {
        var (_, tle) = Pair(name);
        var passes = PredictWeek(tle, tle.Epoch);
        var numbers = OrbitNumberCalculator.ComputeRevolutionNumbers(tle, passes.Select(p => p.AOS).ToList());

        // The old floor((AOS - epoch) / period) formula gave two passes the same number when they
        // were less than one period apart (~93 min vs a ~95 min period). Make sure the fixture
        // really contains such pairs, then that each pass in it gets its own number.
        double periodMinutes = 1440.0 / tle.MeanMotion;
        Assert.Contains(Enumerable.Range(1, passes.Count - 1),
            i => (passes[i].AOS - passes[i - 1].AOS).TotalMinutes < periodMinutes);

        for (int i = 1; i < numbers.Count; i++)
            Assert.True(numbers[i] > numbers[i - 1],
                $"Pass at {passes[i].AOS:u} got orbit {numbers[i]}, previous pass got {numbers[i - 1]}.");
    }

    // ── Stability ────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Pairs))]
    public void SamePass_ComputedFromConsecutiveTles_GetsSameOrbitNumber(string name)
    {
        var (older, newer) = Pair(name);
        var fromOlder = PredictWeek(older, newer.Epoch);
        var fromNewer = PredictWeek(newer, newer.Epoch);
        var olderNumbers = OrbitNumberCalculator.ComputeRevolutionNumbers(older, fromOlder.Select(p => p.AOS).ToList());
        var newerNumbers = OrbitNumberCalculator.ComputeRevolutionNumbers(newer, fromNewer.Select(p => p.AOS).ToList());

        int matched = 0;
        for (int k = 0; k < fromNewer.Count; k++)
        {
            // The same physical pass predicted from two TLEs differs by seconds, not minutes.
            int j = fromOlder.FindIndex(p => Math.Abs((p.AOS - fromNewer[k].AOS).TotalMinutes) < 5);
            if (j < 0) continue;

            matched++;
            Assert.Equal(olderNumbers[j], newerNumbers[k]);
        }

        Assert.True(matched >= 10, $"Expected most passes to be predicted by both TLEs, matched only {matched}.");
    }
}
