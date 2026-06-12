using SatelliteTracker.PassService.SGP4;
using Xunit;

namespace SatelliteTracker.Tests.PassService;

public class TleParserTests
{
    // ISS (NORAD 25544) — checksums verified by formula
    private const string IssLine1 = "1 25544U 98067A   21275.52333333  .00001234  00000-0  29279-4 0  9995";
    private const string IssLine2 = "2 25544  51.6443 213.0093 0004099  83.6831 276.4595 15.48919800303249";

    // EROS C3 — checksums verified by formula
    private const string ErosLine1 = "1 42063U 17004A   21275.50000000  .00000050  00000-0  11111-4 0  9993";
    private const string ErosLine2 = "2 42063  97.9000  30.0000 0001200  90.0000 270.0000 14.81400000  1230";

    [Fact]
    public void Parse_ValidIssTle_ReturnsCorrectSatelliteNumber()
    {
        var tle = TleParser.Parse(IssLine1, IssLine2);
        Assert.Equal(25544, tle.SatelliteNumber);
    }

    [Fact]
    public void Parse_ValidIssTle_ReturnsCorrectClassification()
    {
        var tle = TleParser.Parse(IssLine1, IssLine2);
        Assert.Equal('U', tle.Classification);
    }

    [Fact]
    public void Parse_ValidIssTle_ReturnsCorrectInclination()
    {
        var tle = TleParser.Parse(IssLine1, IssLine2);
        Assert.Equal(51.6443, tle.Inclination, 3);
    }

    [Fact]
    public void Parse_ValidIssTle_ReturnsCorrectEccentricity()
    {
        var tle = TleParser.Parse(IssLine1, IssLine2);
        Assert.Equal(0.0004099, tle.Eccentricity, 6);
    }

    [Fact]
    public void Parse_ValidIssTle_ReturnsCorrectMeanMotion()
    {
        var tle = TleParser.Parse(IssLine1, IssLine2);
        Assert.Equal(15.48919800, tle.MeanMotion, 5);
    }

    [Fact]
    public void Parse_ValidIssTle_ReturnsCorrectEpochYear()
    {
        var tle = TleParser.Parse(IssLine1, IssLine2);
        Assert.Equal(2021, tle.Epoch.Year);
    }

    [Fact]
    public void Parse_ValidIssTle_ReturnsCorrectRevolutionNumber()
    {
        var tle = TleParser.Parse(IssLine1, IssLine2);
        Assert.Equal(30324, tle.RevolutionNumber);
    }

    [Fact]
    public void Parse_ValidIssTle_ReturnsCorrectRightAscension()
    {
        var tle = TleParser.Parse(IssLine1, IssLine2);
        Assert.Equal(213.0093, tle.RightAscension, 3);
    }

    [Fact]
    public void Parse_ValidIssTle_ReturnsCorrectArgumentOfPerigee()
    {
        var tle = TleParser.Parse(IssLine1, IssLine2);
        Assert.Equal(83.6831, tle.ArgumentOfPerigee, 3);
    }

    [Fact]
    public void Parse_ValidIssTle_ReturnsCorrectMeanAnomaly()
    {
        var tle = TleParser.Parse(IssLine1, IssLine2);
        Assert.Equal(276.4595, tle.MeanAnomaly, 3);
    }

    [Fact]
    public void Parse_InvalidChecksumLine1_ThrowsTleParseException()
    {
        // Change last char (checksum digit) to wrong value
        string badLine1 = IssLine1[..^1] + "0";
        Assert.Throws<TleParseException>(() => TleParser.Parse(badLine1, IssLine2));
    }

    [Fact]
    public void Parse_InvalidChecksumLine2_ThrowsTleParseException()
    {
        string badLine2 = IssLine2[..^1] + "0";
        Assert.Throws<TleParseException>(() => TleParser.Parse(IssLine1, badLine2));
    }

    [Fact]
    public void Parse_TooShortLine1_ThrowsTleParseException()
    {
        Assert.Throws<TleParseException>(() => TleParser.Parse("1 25544", IssLine2));
    }

    [Fact]
    public void Parse_TooShortLine2_ThrowsTleParseException()
    {
        Assert.Throws<TleParseException>(() => TleParser.Parse(IssLine1, "2 25544"));
    }

    [Fact]
    public void Parse_NullLine1_ThrowsTleParseException()
    {
        Assert.Throws<TleParseException>(() => TleParser.Parse(null!, IssLine2));
    }

    [Fact]
    public void Parse_MismatchedSatelliteNumbers_ThrowsTleParseException()
    {
        // Build a line1 with satellite number 99999 that has a valid checksum
        // We'll use a different approach: use two valid TLEs but swap the lines
        // ISS line1 has satnum 25544, EROS line2 has satnum 42063
        // Their checksums are each valid for their own line,
        // but satnum mismatch should throw before checksum of line2 is checked with line1's satnum
        // Actually both lines are checked individually. So we need mismatched lines with valid individual checksums.
        // Use IssLine1 with ErosLine2: satnum 25544 vs 42063 — mismatch
        Assert.Throws<TleParseException>(() => TleParser.Parse(IssLine1, ErosLine2));
    }

    [Fact]
    public void Parse_ValidErosTle_ReturnsCorrectSatelliteNumber()
    {
        var tle = TleParser.Parse(ErosLine1, ErosLine2);
        Assert.Equal(42063, tle.SatelliteNumber);
    }

    [Fact]
    public void Parse_ValidIssTle_BStarIsNotNaN()
    {
        var tle = TleParser.Parse(IssLine1, IssLine2);
        Assert.False(double.IsNaN(tle.BStarDrag));
    }

    [Fact]
    public void Parse_ValidIssTle_EpochIsUtc()
    {
        var tle = TleParser.Parse(IssLine1, IssLine2);
        Assert.Equal(DateTimeKind.Utc, tle.Epoch.Kind);
    }
}
