namespace SatelliteTracker.Database.Entities;

public class TleRecord
{
    public Guid Id { get; set; }
    public Guid SatelliteId { get; set; }
    public string Line1 { get; set; } = string.Empty;
    public string Line2 { get; set; } = string.Empty;
    public DateTime Epoch { get; set; }
    public DateTime FetchedAt { get; set; }

    public Satellite Satellite { get; set; } = null!;
    public ICollection<Pass> Passes { get; set; } = [];
}
