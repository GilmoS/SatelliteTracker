namespace SatelliteTracker.Database.Entities;
//This class represents the single global application settings row.
//AlertMinutes and FcmToken moved to the per-tester UserSettings table (beta multi-tester model);
//OutlookDays and TeamEmail remain here, unused for now, reserved for a future Graph API integration.
public class Settings
{
    public Guid Id { get; set; } // Primary key
    public int OutlookDays { get; set; } // Number of days in advance to sync passes with Outlook calendar
    public string? TeamEmail { get; set; } // Email address for the team or group associated with the user (optional)
    public decimal MinElevation { get; set; } = 5; // Minimum elevation in degrees for a pass to be considered for notifications (default is 5 degrees)
    public DateTime UpdatedAt { get; set; } // Timestamp for when the settings were last updated
}
