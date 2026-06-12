using System.Text.Json.Serialization;

namespace SatelliteTracker.TLEService.Client.DTOs;

public class PositionResponse
{
    [JsonPropertyName("info")]
    public PositionInfo Info { get; set; } = new();

    [JsonPropertyName("positions")]
    public PositionData[] Positions { get; set; } = [];
}

public class PositionInfo
{
    [JsonPropertyName("satname")]
    public string SatName { get; set; } = string.Empty;

    [JsonPropertyName("satid")]
    public int SatId { get; set; }

    [JsonPropertyName("transactionscount")]
    public int TransactionsCount { get; set; }
}
