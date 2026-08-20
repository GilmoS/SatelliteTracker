namespace SatelliteTracker.Database.Entities;

//This class represents a satellite pass, which is a specific instance of a satellite being visible from a location on Earth.
//It contains information about the satellite, the TLE record used for calculations, the pass timings, and other related data.
//This allows tracking and managing satellite passes for observation or other purposes.
public class Pass
{
    public Guid Id { get; set; } // Primary key
    public Guid SatelliteId { get; set; } // Foreign key to the Satellite entity
    public Guid TleId { get; set; } // Foreign key to the TleRecord entity
    public int OrbitNumber { get; set; }
    public DateTime Aos { get; set; }
    public DateTime Los { get; set; }
    public decimal MaxElevation { get; set; }
    public decimal AosAzimuth { get; set; }
    public decimal LosAzimuth { get; set; }
    public int DurationSec { get; set; }
    public bool OutlookSynced { get; set; } // Indicates whether the pass has been synced with Outlook calendar
    public DateTime CalculatedAt { get; set; }

    public Satellite Satellite { get; set; } = null!; // Navigation property to the Satellite entity
    public TleRecord TleRecord { get; set; } = null!;// Navigation property to the TleRecord entity
    public ICollection<Note> Notes { get; set; } = [];// Navigation property to the collection of associated Note entities
    public ICollection<PassSubscription> PassSubscriptions { get; set; } = [];// Per-tester notify opt-outs (sparse — see PassSubscription)
    public ICollection<PassNotificationLog> PassNotificationLogs { get; set; } = [];// Per-tester, per-threshold sent-notification ledger
}
