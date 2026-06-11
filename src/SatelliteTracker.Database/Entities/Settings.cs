namespace SatelliteTracker.Database.Entities;

public class Settings
{
    public Guid Id { get; set; }
    public int[] AlertMinutes { get; set; } = [5, 10, 30];
    public int OutlookDays { get; set; }
    public string? TeamEmail { get; set; }
    public decimal MinElevation { get; set; } = 5;
    public string? FcmToken { get; set; }
    public DateTime UpdatedAt { get; set; }
}
