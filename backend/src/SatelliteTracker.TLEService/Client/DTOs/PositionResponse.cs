using System.Text.Json.Serialization;

namespace SatelliteTracker.TLEService.Client.DTOs;

// This class represents the response from the TLE service when requesting satellite position data.
public class PositionResponse
{
    [JsonPropertyName("info")]
    public PositionInfo Info { get; set; } = new();

    [JsonPropertyName("positions")]
    public PositionData[] Positions { get; set; } = [];
}

// This class represents the information about the satellite, including its name, ID, and the count of transactions.
public class PositionInfo
{
    [JsonPropertyName("satname")]
    public string SatName { get; set; } = string.Empty;

    [JsonPropertyName("satid")]
    public int SatId { get; set; }

    [JsonPropertyName("transactionscount")]
    public int TransactionsCount { get; set; }
}
