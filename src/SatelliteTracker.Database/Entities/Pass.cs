namespace SatelliteTracker.Database.Entities;

public class Pass
{
    public Guid Id { get; set; }
    public Guid SatelliteId { get; set; }
    public Guid TleId { get; set; }
    public int OrbitNumber { get; set; }
    public DateTime Aos { get; set; }
    public DateTime Los { get; set; }
    public decimal MaxElevation { get; set; }
    public decimal AosAzimuth { get; set; }
    public decimal LosAzimuth { get; set; }
    public int DurationSec { get; set; }
    public bool NotificationSent { get; set; }
    public DateTime? NotificationSentAt { get; set; }
    public bool OutlookSynced { get; set; }
    public DateTime CalculatedAt { get; set; }

    public Satellite Satellite { get; set; } = null!;
    public TleRecord TleRecord { get; set; } = null!;
    public ICollection<Note> Notes { get; set; } = [];
}
