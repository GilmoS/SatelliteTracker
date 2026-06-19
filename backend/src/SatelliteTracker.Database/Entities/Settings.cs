namespace SatelliteTracker.Database.Entities;
//This class represents the user settings for the satellite tracking application.
//It contains various preferences and configurations that the user can set, such as alert timings, Outlook calendar integration, and minimum elevation for notifications.
public class Settings
{
    public Guid Id { get; set; } // Primary key
    public int[] AlertMinutes { get; set; } = [5, 10, 30]; // Array of minutes before a pass when the user wants to receive alerts
    public int OutlookDays { get; set; } // Number of days in advance to sync passes with Outlook calendar
    public string? TeamEmail { get; set; } // Email address for the team or group associated with the user (optional)
    public decimal MinElevation { get; set; } = 5; // Minimum elevation in degrees for a pass to be considered for notifications (default is 5 degrees)
    public string? FcmToken { get; set; } // Firebase Cloud Messaging token for push notifications (optional)
    public DateTime UpdatedAt { get; set; } // Timestamp for when the settings were last updated
}
