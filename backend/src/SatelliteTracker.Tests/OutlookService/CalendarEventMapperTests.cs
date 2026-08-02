using SatelliteTracker.Database.Entities;
using SatelliteTracker.OutlookService.Mapping;
using Xunit;

namespace SatelliteTracker.Tests.OutlookService;

public class CalendarEventMapperTests
{
    private static Pass BuildPass(Satellite? satellite, int orbitNumber = 1234)
    {
        return new Pass
        {
            Id = Guid.NewGuid(),
            SatelliteId = satellite?.Id ?? Guid.NewGuid(),
            TleId = Guid.NewGuid(),
            OrbitNumber = orbitNumber,
            Aos = new DateTime(2026, 8, 1, 11, 0, 0, DateTimeKind.Utc),
            Los = new DateTime(2026, 8, 1, 11, 10, 0, DateTimeKind.Utc),
            MaxElevation = 45.3m,
            AosAzimuth = 120.5m,
            LosAzimuth = 200.1m,
            DurationSec = 600,
            CalculatedAt = DateTime.UtcNow,
            Satellite = satellite!
        };
    }

    [Fact]
    public void ToCalendarEventData_MapsAllFields_WhenSatelliteIsLoaded()
    {
        var satellite = new Satellite
        {
            Id = Guid.NewGuid(),
            Name = "EROS C3",
            NoradId = 25544,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var pass = BuildPass(satellite);

        var result = pass.ToCalendarEventData();

        Assert.Equal("25544-1234@sattrakk.com", result.Uid);
        Assert.Equal("EROS C3", result.SatelliteName);
        Assert.Equal(25544, result.NoradId);
        Assert.Equal(1234, result.OrbitNumber);
        Assert.Equal(new DateTimeOffset(pass.Aos, TimeSpan.Zero), result.Aos);
        Assert.Equal(new DateTimeOffset(pass.Los, TimeSpan.Zero), result.Los);
        Assert.Equal(45.3m, result.MaxElevation);
        Assert.Equal(120.5m, result.AosAzimuth);
        Assert.Equal(200.1m, result.LosAzimuth);
    }

    [Fact]
    public void ToCalendarEventData_Throws_WhenSatelliteIsNull()
    {
        var pass = BuildPass(null);

        Assert.Throws<InvalidOperationException>(() => pass.ToCalendarEventData());
    }

    [Fact]
    public void ToCalendarEventData_DifferentOrbitNumbers_ProduceDifferentUids()
    {
        var satellite = new Satellite
        {
            Id = Guid.NewGuid(),
            Name = "EROS C3",
            NoradId = 25544,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var passA = BuildPass(satellite, orbitNumber: 1234);
        var passB = BuildPass(satellite, orbitNumber: 1235);

        var uidA = passA.ToCalendarEventData().Uid;
        var uidB = passB.ToCalendarEventData().Uid;

        Assert.NotEqual(uidA, uidB);
    }
}
