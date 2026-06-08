namespace SatelliteTracker.API.Modules.Database.Entities;

public class Pass
{
    public Guid Id { get; set; } // Primary key for the Pass entity
    public Guid SatelliteId { get; set; } // Foreign key referencing the related Satellite
    public Guid TleId { get; set; } // Foreign key referencing the related TleRecord
    public int OrbitNumber { get; set; } // The orbit number for this pass, which can be used to identify the specific pass in a series of passes
    public DateTime Aos { get; set; } // Acquisition of Signal (AOS) time, when the satellite first becomes visible above the horizon
    public DateTime Los { get; set; } // Loss of Signal (LOS) time, when the satellite is no longer visible above the horizon
    public decimal MaxElevation { get; set; } // Maximum elevation angle of the satellite during the pass, measured in degrees
    public decimal AosAzimuth { get; set; } // Azimuth at Acquisition of Signal (AOS)
    public decimal LosAzimuth { get; set; } // Azimuth at Loss of Signal (LOS)
    public int DurationSec { get; set; } // Duration of the pass in seconds
    public bool NotificationSent { get; set; } // Whether a notification has been sent for this pass
    public DateTime? NotificationSentAt { get; set; } // Timestamp of when the notification was sent
    public bool OutlookSynced { get; set; } // Whether the pass has been synced with Outlook
    public DateTime CalculatedAt { get; set; } // Timestamp of when the pass was calculated

    public Satellite Satellite { get; set; } = null!; // Navigation property to the related Satellite
    public TleRecord TleRecord { get; set; } = null!; // Navigation property to the related TleRecord
    public ICollection<Note> Notes { get; set; } = []; // Collection of notes associated with this pass
}