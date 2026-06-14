namespace SatelliteTracker.PassService.SGP4;


// This class provides a method to parse Two-Line Element (TLE) data from two strings, extracting the orbital parameters and returning a TleData object.
// It also validates the format and checksum of the TLE lines, and throws TleParseException if any issues are found during parsing.
public static class TleParser
{

    // Parses two lines of TLE data and returns a TleData object containing the extracted parameters.
   // TLE is sourced from N2YO (as requested by client), and the lines must conform to the expected format.
    public static TleData Parse(string line1, string line2)
    {
        if (string.IsNullOrWhiteSpace(line1) || line1.Length < 69) // TlE lines are expected to be 69 characters long
            throw new TleParseException("TLE line 1 is invalid or too short.");

        if (string.IsNullOrWhiteSpace(line2) || line2.Length < 69)
            throw new TleParseException("TLE line 2 is invalid or too short.");

        // Validate the checksum of both lines to ensure data integrity
        ValidateChecksum(line1, 1);
        ValidateChecksum(line2, 2);

        // Validate that the first character of line 1 is '1' and line 2 is '2', as per TLE format specifications
        if (line1[0] != '1')
            throw new TleParseException("TLE line 1 must start with '1'.");
        if (line2[0] != '2')
            throw new TleParseException("TLE line 2 must start with '2'.");

        try
        {
            int satNum1 = ParseInt(line1, 2, 5); // Satellite number is located at positions 3-7 in line 1
            int satNum2 = ParseInt(line2, 2, 5); // Satellite number is located at positions 3-7 in line 2

            if (satNum1 != satNum2)
                throw new TleParseException("Satellite numbers in line 1 and line 2 do not match.");

            // Extract other parameters from the TLE lines using substring and parsing methods
            char classification = line1[7]; // Classification character is located at position 8 in line 1
            string intlDesignator = line1.Substring(9, 8).Trim(); // International designator is located at positions 10-17 in line 1
            DateTime epoch = ParseEpoch(line1.Substring(18, 14).Trim()); // Epoch is located at positions 19-32 in line 1
            double meanMotionDot = ParseDouble(line1.Substring(33, 10).Trim()); // Mean motion derivative is located at positions 34-43 in line 1
            double meanMotionDotDot = ParseDecimalPoint(line1.Substring(44, 8).Trim()); // Second derivative of mean motion is located at positions 45-52 in line 1
            double bstar = ParseDecimalPoint(line1.Substring(53, 8).Trim()); // B* drag term is located at positions 54-61 in line 1

            double inclination = ParseDouble(line2.Substring(8, 8).Trim()); // Inclination is located at positions 9-16 in line 2
            double raan = ParseDouble(line2.Substring(17, 8).Trim()); // Right ascension of ascending node is located at positions 18-25 in line 2
            double eccentricity = double.Parse("0." + line2.Substring(26, 7).Trim()); // Eccentricity is located at positions 27-33 in line 2
            double argPerigee = ParseDouble(line2.Substring(34, 8).Trim()); // Argument of perigee is located at positions 35-42 in line 2
            double meanAnomaly = ParseDouble(line2.Substring(43, 8).Trim()); // Mean anomaly is located at positions 44-51 in line 2
            double meanMotion = ParseDouble(line2.Substring(52, 11).Trim()); // Mean motion is located at positions 53-63 in line 2
            int revNum = ParseInt(line2, 63, 5); // Revolution number is located at positions 64-68 in line 2

            // Create and return a TleData object with the parsed parameters
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
            throw; // Rethrow TleParseException without wrapping to preserve original error details
        }
        catch (Exception ex)
        {
            throw new TleParseException($"Failed to parse TLE data: {ex.Message}", ex);
        }
    }

    // Validates the checksum of a TLE line by calculating the sum of digits and dashes, and comparing it to the expected checksum value.
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

    // Parses the epoch from the TLE line, which is in the format YYDDDddddddddd (2-digit year, day of year with fractional part),
    // and converts it to a DateTime object.
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

    // Parses a double value from a string using invariant culture to ensure consistent decimal point handling regardless of locale settings.
    private static double ParseDouble(string s) => double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

    // Parses an integer value from a substring of the TLE line, trimming any whitespace before parsing.
    private static int ParseInt(string line, int startIndex, int length)
    {
        string sub = line.Substring(startIndex, length).Trim();
        return int.Parse(sub);
    }
}
