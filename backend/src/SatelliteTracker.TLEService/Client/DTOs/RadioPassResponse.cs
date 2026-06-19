using System.Text.Json.Serialization;

namespace SatelliteTracker.TLEService.Client.DTOs;
// This class represents the response from the TLE service when requesting radio pass data for a satellite.
public class RadioPassResponse
{
    [JsonPropertyName("info")]
    public RadioPassInfo Info { get; set; } = new();

    [JsonPropertyName("passes")]
    public RadioPassData[] Passes { get; set; } = [];
}
// This class represents the information about the satellite for radio pass data, including its name, ID, and the count of transactions and passes.
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
