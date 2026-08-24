using SatelliteTracker.PassService.SGP4;

namespace SatelliteTracker.API.DTOs;

public class PassTrackDto
{
    public Guid PassId { get; set; }
    public List<PassTrackPointDto> Points { get; set; } = [];
}

public class PassTrackPointDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }
    public long Timestamp { get; set; }

    public static PassTrackPointDto From(GroundTrackPoint p) => new()
    {
        Latitude = p.Latitude,
        Longitude = p.Longitude,
        Altitude = p.Altitude,
        Timestamp = new DateTimeOffset(DateTime.SpecifyKind(p.TimestampUtc, DateTimeKind.Utc)).ToUnixTimeSeconds()
    };
}
