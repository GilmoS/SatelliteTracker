using System.Text.Json.Serialization;

namespace SatelliteTracker.TLEService.Client.DTOs;

public class RadioPassData
{
    [JsonPropertyName("startAz")]
    public double StartAz { get; set; }

    [JsonPropertyName("startAzCompass")]
    public string StartAzCompass { get; set; } = string.Empty;

    [JsonPropertyName("startUTC")]
    public long StartUTC { get; set; }

    [JsonPropertyName("maxAz")]
    public double MaxAz { get; set; }

    [JsonPropertyName("maxAzCompass")]
    public string MaxAzCompass { get; set; } = string.Empty;

    [JsonPropertyName("maxEl")]
    public double MaxEl { get; set; }

    [JsonPropertyName("maxUTC")]
    public long MaxUTC { get; set; }

    [JsonPropertyName("endAz")]
    public double EndAz { get; set; }

    [JsonPropertyName("endAzCompass")]
    public string EndAzCompass { get; set; } = string.Empty;

    [JsonPropertyName("endUTC")]
    public long EndUTC { get; set; }

    [JsonPropertyName("duration")]
    public int Duration { get; set; }
}
