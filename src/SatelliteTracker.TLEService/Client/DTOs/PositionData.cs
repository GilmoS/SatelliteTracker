using System.Text.Json.Serialization;

namespace SatelliteTracker.TLEService.Client.DTOs;

public class PositionData
{
    [JsonPropertyName("satlatitude")]
    public double SatLatitude { get; set; }

    [JsonPropertyName("satlongitude")]
    public double SatLongitude { get; set; }

    [JsonPropertyName("sataltitude")]
    public double SatAltitude { get; set; }

    [JsonPropertyName("azimuth")]
    public double Azimuth { get; set; }

    [JsonPropertyName("elevation")]
    public double Elevation { get; set; }

    [JsonPropertyName("ra")]
    public double Ra { get; set; }

    [JsonPropertyName("dec")]
    public double Dec { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
}
