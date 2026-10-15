namespace SatelliteTracker.PassService.SGP4;

// Computes a satellite's revolution (orbit) number at a given time, using the convention TLEs
// use: the count increments at each ascending node (northbound equator crossing). The result is
// the TLE's revolution number at epoch plus the ascending nodes between the TLE epoch and that
// time, found by SGP4 propagation.
//
// Counting from the ascending node, not from whole periods since epoch, is what keeps the number
// unique and stable per physical pass: a pass over Israel (~31° N) never has its AOS near the
// equator, so two consecutive passes always fall in different revolutions, and every TLE agrees
// on which revolution a given pass belongs to.
public static class OrbitNumberCalculator
{
    // A sign change in Z is counted per sample interval, so the step must stay well under half an
    // orbital period (~47 min for LEO) to guarantee no crossing is skipped and no interval holds two.
    private const int StepMinutes = 10;

    // The TLEs we receive have their epoch exactly on an ascending node (every stored set, to within
    // 0.01 s). That node is the start of the revolution the TLE's number already counts, so a node
    // within this window of the epoch is treated as the epoch's own and never counted again —
    // otherwise float noise putting Z a hair below zero at epoch would add 1 to every pass.
    private static readonly TimeSpan EpochNodeTolerance = TimeSpan.FromSeconds(60);

    public static int ComputeRevolutionNumber(TleData tle, DateTime utcTime)
        => ComputeRevolutionNumbers(tle, [utcTime])[0];

    // Computes the revolution number for each time, returned in input order. Times after the TLE
    // epoch share a single forward sweep, so a week of passes costs one propagation from the epoch
    // rather than one per pass.
    public static IReadOnlyList<int> ComputeRevolutionNumbers(TleData tle, IReadOnlyList<DateTime> utcTimes)
    {
        // Revolution RevolutionNumber starts at the epoch node when there is one. Counting then
        // starts/stops a tolerance away from it, where Z is clearly non-zero, so the epoch node
        // itself can't be counted.
        var epochNode = FindNodeNearEpoch(tle);
        DateTime sweepStart = epochNode.HasValue ? epochNode.Value + EpochNodeTolerance : tle.Epoch;

        var results = new int[utcTimes.Count];
        var forward = new List<int>();

        for (int i = 0; i < utcTimes.Count; i++)
        {
            DateTime t = utcTimes[i];
            if (t >= sweepStart)
                forward.Add(i);
            else if (epochNode is null)
                results[i] = tle.RevolutionNumber - CountAscendingNodes(tle, t, tle.Epoch);
            else if (t >= epochNode.Value)
                results[i] = tle.RevolutionNumber;
            else
            {
                DateTime to = epochNode.Value - EpochNodeTolerance;
                results[i] = t >= to
                    ? tle.RevolutionNumber - 1
                    : tle.RevolutionNumber - 1 - CountAscendingNodes(tle, t, to);
            }
        }

        // Forward sweep: for each target in time order, count nodes in (sweepStart, target].
        int count = 0;
        DateTime sample = sweepStart;
        double previousZ = Z(tle, sample);

        foreach (int i in forward.OrderBy(i => utcTimes[i]))
        {
            count += CountAscendingNodes(tle, ref sample, ref previousZ, utcTimes[i]);
            results[i] = tle.RevolutionNumber + count;
        }

        return results;
    }

    // Returns the ascending node within ±EpochNodeTolerance of the TLE epoch, refined by bisection,
    // or null if there isn't one.
    private static DateTime? FindNodeNearEpoch(TleData tle)
    {
        DateTime lo = tle.Epoch - EpochNodeTolerance;
        DateTime hi = tle.Epoch + EpochNodeTolerance;

        if (!(Z(tle, lo) < 0 && Z(tle, hi) >= 0))
            return null;

        while ((hi - lo).TotalSeconds > 0.01)
        {
            DateTime mid = lo + TimeSpan.FromTicks((hi - lo).Ticks / 2);
            if (Z(tle, mid) < 0) lo = mid;
            else hi = mid;
        }

        return hi;
    }

    // Counts ascending nodes in (fromUtc, toUtc]: sample intervals where the ECI Z coordinate goes
    // from negative to non-negative. Both endpoints are sampled exactly.
    private static int CountAscendingNodes(TleData tle, DateTime fromUtc, DateTime toUtc)
    {
        double previousZ = Z(tle, fromUtc);
        return CountAscendingNodes(tle, ref fromUtc, ref previousZ, toUtc);
    }

    // Advances sample (whose Z is previousZ) to toUtc, returning the nodes crossed on the way.
    private static int CountAscendingNodes(TleData tle, ref DateTime sample, ref double previousZ, DateTime toUtc)
    {
        int count = 0;

        while (sample < toUtc)
        {
            sample = sample.AddMinutes(StepMinutes);
            if (sample > toUtc) sample = toUtc;

            double z = Z(tle, sample);
            if (previousZ < 0 && z >= 0)
                count++;
            previousZ = z;
        }

        return count;
    }

    private static double Z(TleData tle, DateTime utcTime)
        => Sgp4Calculator.CalculatePositionVelocity(tle, utcTime).Z;
}
