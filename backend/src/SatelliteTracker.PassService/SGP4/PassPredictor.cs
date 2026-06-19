namespace SatelliteTracker.PassService.SGP4;


// This class contains the main logic for predicting satellite passes over a ground station using the SGP4 algorithm.
public static class PassPredictor
{
    // Predicts satellite passes over a ground station within a specified time range and minimum elevation.
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
        var obs = Sgp4Calculator.CalculateObserverPosition(observerLat, observerLng, observerAltM); // Precompute observer position for efficiency
        var passes = new List<PassResult>(); // List to hold the predicted passes

        const int coarseStepSeconds = 30;
        DateTime t = fromUtc;
        bool inPass = false;
        DateTime aosTime = default;
        double aosAzimuth = 0;
        double maxElev = 0;
        DateTime maxElevTime = default;


        while (t <= toUtc) // Coarse step to find potential passes
        {
            double elev = GetElevation(tle, obs, t);

            if (!inPass && elev >= minElevationDeg) // If the satellite is rising above the minimum elevation, refine the AOS (Acquisition of Signal)
            {
                // Refine AOS to 1-second precision, not before fromUtc
                DateTime searchStart = t.AddSeconds(-coarseStepSeconds);
                if (searchStart < fromUtc) searchStart = fromUtc;

                // If the coarse step already found the AOS, no need to refine
                DateTime aosRefined = searchStart == t ? t: RefineTime(tle, obs, searchStart, t, minElevationDeg, rising: true);

                // Refine max elevation between AOS and the current time
                var aosLook = GetLookAngles(tle, obs, aosRefined);
                aosTime = aosRefined;
                aosAzimuth = aosLook.Azimuth;
                maxElev = elev;
                maxElevTime = t;
                inPass = true;
            }
            else if (inPass) // If the satellite is currently in a pass, check for LOS (Loss of Signal)
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

                    if (duration > 0) // Only add passes with a positive duration
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

        return passes; // Return the list of predicted passes
    }

    // Helper method to get the elevation of the satellite at a specific time for a given observer position.
    private static double GetElevation(TleData tle, ObserverPosition obs, DateTime time)
    {
        var pv = Sgp4Calculator.CalculatePositionVelocity(tle, time);
        var look = Sgp4Calculator.CalculateLookAngles(pv, obs, time);
        return look.Elevation;
    }

    // Helper method to get the look angles (elevation and azimuth) of the satellite at a specific time for a given observer position.
    private static LookAngles GetLookAngles(TleData tle, ObserverPosition obs, DateTime time)
    {
        var pv = Sgp4Calculator.CalculatePositionVelocity(tle, time);
        return Sgp4Calculator.CalculateLookAngles(pv, obs, time);
    }

    // Refines the time of AOS or LOS to 1-second precision using binary search.
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

    // Finds the maximum elevation of the satellite between two times using a ternary search.
    private static double FindMaxElevation(TleData tle, ObserverPosition obs, DateTime start, DateTime end)
    {
        // Ternary search for maximum elevation
        DateTime lo = start, hi = end;
        while ((hi - lo).TotalSeconds > 1) // Continue until the interval is less than 1 second
        {
            DateTime m1 = lo + TimeSpan.FromSeconds((hi - lo).TotalSeconds / 3);
            DateTime m2 = hi - TimeSpan.FromSeconds((hi - lo).TotalSeconds / 3);
            double e1 = GetElevation(tle, obs, m1);
            double e2 = GetElevation(tle, obs, m2);
            if (e1 < e2)
                lo = m1; 
            else
                hi = m2;
        }

        return GetElevation(tle, obs, lo + TimeSpan.FromSeconds((hi - lo).TotalSeconds / 2));
    }
}
