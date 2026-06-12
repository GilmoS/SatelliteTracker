namespace SatelliteTracker.PassService.SGP4;

public static class PassPredictor
{
    public static IEnumerable<PassResult> PredictPasses(
        TleData tle,
        Guid satelliteId,
        double observerLat,
        double observerLng,
        double observerAltM,
        DateTime fromUtc,
        DateTime toUtc,
        double minElevationDeg = 5.0)
    {
        var obs = Sgp4Calculator.CalculateObserverPosition(observerLat, observerLng, observerAltM);
        var passes = new List<PassResult>();

        const int coarseStepSeconds = 30;
        DateTime t = fromUtc;
        bool inPass = false;
        DateTime aosTime = default;
        double aosAzimuth = 0;
        double maxElev = 0;
        DateTime maxElevTime = default;

        while (t <= toUtc)
        {
            double elev = GetElevation(tle, obs, t);

            if (!inPass && elev >= minElevationDeg)
            {
                // Refine AOS to 1-second precision, not before fromUtc
                DateTime searchStart = t.AddSeconds(-coarseStepSeconds);
                if (searchStart < fromUtc) searchStart = fromUtc;
                DateTime aosRefined = searchStart == t ? t
                    : RefineTime(tle, obs, searchStart, t, minElevationDeg, rising: true);
                var aosLook = GetLookAngles(tle, obs, aosRefined);
                aosTime = aosRefined;
                aosAzimuth = aosLook.Azimuth;
                maxElev = elev;
                maxElevTime = t;
                inPass = true;
            }
            else if (inPass)
            {
                if (elev > maxElev)
                {
                    maxElev = elev;
                    maxElevTime = t;
                }

                if (elev < minElevationDeg)
                {
                    // Refine LOS to 1-second precision
                    DateTime losRefined = RefineTime(tle, obs, t.AddSeconds(-coarseStepSeconds), t, minElevationDeg, rising: false);

                    // Refine max elevation between AOS and LOS
                    double refinedMaxElev = FindMaxElevation(tle, obs, aosTime, losRefined);

                    var losLook = GetLookAngles(tle, obs, losRefined);
                    int duration = (int)(losRefined - aosTime).TotalSeconds;

                    if (duration > 0)
                    {
                        passes.Add(new PassResult(
                            satelliteId,
                            aosTime,
                            losRefined,
                            refinedMaxElev,
                            aosAzimuth,
                            losLook.Azimuth,
                            duration));
                    }

                    inPass = false;
                    maxElev = 0;
                }
            }

            t = t.AddSeconds(coarseStepSeconds);
        }

        return passes;
    }

    private static double GetElevation(TleData tle, ObserverPosition obs, DateTime time)
    {
        var pv = Sgp4Calculator.CalculatePositionVelocity(tle, time);
        var look = Sgp4Calculator.CalculateLookAngles(pv, obs, time);
        return look.Elevation;
    }

    private static LookAngles GetLookAngles(TleData tle, ObserverPosition obs, DateTime time)
    {
        var pv = Sgp4Calculator.CalculatePositionVelocity(tle, time);
        return Sgp4Calculator.CalculateLookAngles(pv, obs, time);
    }

    private static DateTime RefineTime(TleData tle, ObserverPosition obs,
        DateTime start, DateTime end, double threshold, bool rising)
    {
        // Binary search to 1-second precision
        while ((end - start).TotalSeconds > 1)
        {
            DateTime mid = start + TimeSpan.FromSeconds((end - start).TotalSeconds / 2);
            double elev = GetElevation(tle, obs, mid);

            if (rising ? elev < threshold : elev >= threshold)
                start = mid;
            else
                end = mid;
        }

        return rising ? end : start;
    }

    private static double FindMaxElevation(TleData tle, ObserverPosition obs, DateTime start, DateTime end)
    {
        // Ternary search for maximum elevation
        DateTime lo = start, hi = end;
        while ((hi - lo).TotalSeconds > 1)
        {
            DateTime m1 = lo + TimeSpan.FromSeconds((hi - lo).TotalSeconds / 3);
            DateTime m2 = hi - TimeSpan.FromSeconds((hi - lo).TotalSeconds / 3);
            double e1 = GetElevation(tle, obs, m1);
            double e2 = GetElevation(tle, obs, m2);
            if (e1 < e2) lo = m1; else hi = m2;
        }

        return GetElevation(tle, obs, lo + TimeSpan.FromSeconds((hi - lo).TotalSeconds / 2));
    }
}
