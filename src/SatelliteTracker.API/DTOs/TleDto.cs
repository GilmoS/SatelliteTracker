using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.API.DTOs;

public class TleDto
{
    public Guid Id { get; set; }
    public Guid SatelliteId { get; set; }
    public string Line1 { get; set; } = string.Empty;
    public string Line2 { get; set; } = string.Empty;
    public DateTime Epoch { get; set; }
    public DateTime FetchedAt { get; set; }

    public static TleDto From(TleRecord t) => new()
    {
        Id = t.Id,
        SatelliteId = t.SatelliteId,
        Line1 = t.Line1,
        Line2 = t.Line2,
        Epoch = t.Epoch,
        FetchedAt = t.FetchedAt
    };
}
