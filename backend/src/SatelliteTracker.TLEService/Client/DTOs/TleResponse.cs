using System.Text.Json.Serialization;

namespace SatelliteTracker.TLEService.Client.DTOs;

// This class represents the response from the TLE service when requesting TLE data for a satellite.
public class TleResponse
{
    [JsonPropertyName("info")]
    public TleInfo Info { get; set; } = new();

    [JsonPropertyName("tle")]
    public string Tle { get; set; } = string.Empty;
}
// This class represents the information about the satellite for TLE data, including its name, ID, and the count of transactions.
public class TleInfo
{
    [JsonPropertyName("satid")]
    public int SatId { get; set; }

    [JsonPropertyName("satname")]
    public string SatName { get; set; } = string.Empty;

    [JsonPropertyName("transactionscount")]
    public int TransactionsCount { get; set; }
}
