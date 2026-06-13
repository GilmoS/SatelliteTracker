using SatelliteTracker.TLEService.Client.DTOs;

namespace SatelliteTracker.API.DTOs;

public class PositionDto
{
    public int NoradId { get; set; }
    public string SatName { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }
    public double Azimuth { get; set; }
    public double Elevation { get; set; }
    public long Timestamp { get; set; }

    public static PositionDto From(int noradId, string satName, PositionData p) => new()
    {
        NoradId = noradId,
        SatName = satName,
        Latitude = p.SatLatitude,
        Longitude = p.SatLongitude,
        Altitude = p.SatAltitude,
        Azimuth = p.Azimuth,
        Elevation = p.Elevation,
        Timestamp = p.Timestamp
    };
}

public class TrackDto
{
    public int NoradId { get; set; }
    public string SatName { get; set; } = string.Empty;
    public List<TrackPointDto> Points { get; set; } = [];
}

public class TrackPointDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }
    public long Timestamp { get; set; }

    public static TrackPointDto From(PositionData p) => new()
    {
        Latitude = p.SatLatitude,
        Longitude = p.SatLongitude,
        Altitude = p.SatAltitude,
        Timestamp = p.Timestamp
    };
}
