using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.API.DTOs;

public class PassDto
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
    public bool Notify { get; set; }
    public DateTime CalculatedAt { get; set; }

    public static PassDto From(Pass p) => new()
    {
        Id = p.Id,
        SatelliteId = p.SatelliteId,
        TleId = p.TleId,
        OrbitNumber = p.OrbitNumber,
        Aos = p.Aos,
        Los = p.Los,
        MaxElevation = p.MaxElevation,
        AosAzimuth = p.AosAzimuth,
        LosAzimuth = p.LosAzimuth,
        DurationSec = p.DurationSec,
        NotificationSent = p.NotificationSent,
        NotificationSentAt = p.NotificationSentAt,
        OutlookSynced = p.OutlookSynced,
        Notify = p.Notify,
        CalculatedAt = p.CalculatedAt
    };
}
