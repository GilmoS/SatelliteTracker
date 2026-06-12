using System.Text.Json.Serialization;

namespace SatelliteTracker.TLEService.Client.DTOs;

public class RadioPassResponse
{
    [JsonPropertyName("info")]
    public RadioPassInfo Info { get; set; } = new();

    [JsonPropertyName("passes")]
    public RadioPassData[] Passes { get; set; } = [];
}

public class RadioPassInfo
{
    [JsonPropertyName("satid")]
    public int SatId { get; set; }

    [JsonPropertyName("satname")]
    public string SatName { get; set; } = string.Empty;

    [JsonPropertyName("transactionscount")]
    public int TransactionsCount { get; set; }

    [JsonPropertyName("passescount")]
    public int PassesCount { get; set; }
}
