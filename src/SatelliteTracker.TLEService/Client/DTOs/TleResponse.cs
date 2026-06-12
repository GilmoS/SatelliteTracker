using System.Text.Json.Serialization;

namespace SatelliteTracker.TLEService.Client.DTOs;

public class TleResponse
{
    [JsonPropertyName("info")]
    public TleInfo Info { get; set; } = new();

    [JsonPropertyName("tle")]
    public string Tle { get; set; } = string.Empty;
}

public class TleInfo
{
    [JsonPropertyName("satid")]
    public int SatId { get; set; }

    [JsonPropertyName("satname")]
    public string SatName { get; set; } = string.Empty;

    [JsonPropertyName("transactionscount")]
    public int TransactionsCount { get; set; }
}
