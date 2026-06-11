namespace SatelliteTracker.Database.Entities;

public class Satellite
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int NoradId { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<TleRecord> TleRecords { get; set; } = [];
    public ICollection<Pass> Passes { get; set; } = [];
}
