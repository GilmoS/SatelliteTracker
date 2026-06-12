namespace SatelliteTracker.PassService.SGP4;

public static class TleParser
{
    public static TleData Parse(string line1, string line2)
    {
        if (string.IsNullOrWhiteSpace(line1) || line1.Length < 69)
            throw new TleParseException("TLE line 1 is invalid or too short.");
        if (string.IsNullOrWhiteSpace(line2) || line2.Length < 69)
            throw new TleParseException("TLE line 2 is invalid or too short.");

        ValidateChecksum(line1, 1);
        ValidateChecksum(line2, 2);

        if (line1[0] != '1')
            throw new TleParseException("TLE line 1 must start with '1'.");
        if (line2[0] != '2')
            throw new TleParseException("TLE line 2 must start with '2'.");

        try
        {
            int satNum1 = ParseInt(line1, 2, 5);
            int satNum2 = ParseInt(line2, 2, 5);
            if (satNum1 != satNum2)
                throw new TleParseException("Satellite numbers in line 1 and line 2 do not match.");

            char classification = line1[7];
            string intlDesignator = line1.Substring(9, 8).Trim();
            DateTime epoch = ParseEpoch(line1.Substring(18, 14).Trim());
            double meanMotionDot = ParseDouble(line1.Substring(33, 10).Trim());
            double meanMotionDotDot = ParseDecimalPoint(line1.Substring(44, 8).Trim());
            double bstar = ParseDecimalPoint(line1.Substring(53, 8).Trim());

            double inclination = ParseDouble(line2.Substring(8, 8).Trim());
            double raan = ParseDouble(line2.Substring(17, 8).Trim());
            double eccentricity = double.Parse("0." + line2.Substring(26, 7).Trim());
            double argPerigee = ParseDouble(line2.Substring(34, 8).Trim());
            double meanAnomaly = ParseDouble(line2.Substring(43, 8).Trim());
            double meanMotion = ParseDouble(line2.Substring(52, 11).Trim());
            int revNum = ParseInt(line2, 63, 5);

            return new TleData
            {
                SatelliteNumber = satNum1,
                Classification = classification,
                InternationalDesignator = intlDesignator,
                Epoch = epoch,
                MeanMotionDot = meanMotionDot,
                MeanMotionDotDot = meanMotionDotDot,
                BStarDrag = bstar,
                Inclination = inclination,
                RightAscension = raan,
                Eccentricity = eccentricity,
                ArgumentOfPerigee = argPerigee,
                MeanAnomaly = meanAnomaly,
                MeanMotion = meanMotion,
                RevolutionNumber = revNum
            };
        }
        catch (TleParseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new TleParseException($"Failed to parse TLE data: {ex.Message}", ex);
        }
    }

    private static void ValidateChecksum(string line, int lineNumber)
    {
        if (line.Length < 69)
            throw new TleParseException($"TLE line {lineNumber} is too short for checksum validation.");

        int sum = 0;
        for (int i = 0; i < 68; i++)
        {
            char c = line[i];
            if (char.IsDigit(c))
                sum += c - '0';
            else if (c == '-')
                sum += 1;
        }

        int expected = sum % 10;
        int actual = line[68] - '0';

        if (expected != actual)
            throw new TleParseException($"TLE line {lineNumber} checksum mismatch: expected {expected}, got {actual}.");
    }

    private static DateTime ParseEpoch(string epochStr)
    {
        // Format: YYDDDddddddddd (2-digit year, day of year with fractional part)
        int year = int.Parse(epochStr.Substring(0, 2));
        year += year < 57 ? 2000 : 1900;

        double dayOfYear = double.Parse(epochStr.Substring(2));
        int day = (int)dayOfYear;
        double fraction = dayOfYear - day;

        var date = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddDays(day - 1)
            .AddSeconds(fraction * 86400.0);

        return date;
    }

    // Parses the TLE "decimal point assumed" format: +NNNNN-N → 0.NNNNN × 10^N
    private static double ParseDecimalPoint(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s == "00000+0" || s == "00000-0")
            return 0.0;

        // Find the last +/- that is the exponent sign (not the leading sign)
        int sign = s[0] == '-' ? -1 : 1;
        string body = s.TrimStart('+', '-', ' ');

        // body is like: NNNNN-N or NNNNN+N
        // find the exponent sign position (last + or -)
        int expSignIdx = -1;
        for (int i = body.Length - 1; i >= 0; i--)
        {
            if (body[i] == '+' || body[i] == '-')
            {
                expSignIdx = i;
                break;
            }
        }

        if (expSignIdx < 0)
            return 0.0;

        string mantissaStr = body.Substring(0, expSignIdx);
        string exponentStr = body.Substring(expSignIdx);

        double mantissa = double.Parse("0." + mantissaStr);
        int exponent = int.Parse(exponentStr);

        return sign * mantissa * Math.Pow(10, exponent);
    }

    private static double ParseDouble(string s) => double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

    private static int ParseInt(string line, int startIndex, int length)
    {
        string sub = line.Substring(startIndex, length).Trim();
        return int.Parse(sub);
    }
}
