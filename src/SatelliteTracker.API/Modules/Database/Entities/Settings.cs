namespace SatelliteTracker.API.Modules.Database.Entities;

public class Settings
{
    public Guid Id { get; set; } // Primary key for the Settings entity
    public int[] AlertMinutes { get; set; } = [5, 10, 30]; // Array of alert times in minutes before the pass, which can be used to schedule notifications for upcoming passes
    public int OutlookDays { get; set; } // Number of days in advance to sync passes with Outlook, which can be used to ensure that the user's calendar is up-to-date with upcoming satellite passes
    public string? TeamEmail { get; set; } // Optional email address for the user's team, which can be used for sharing pass information
    public decimal MinElevation { get; set; } = 5; // Minimum elevation angle in degrees for a pass to be considered visible, which can be used to filter out passes that are too low on the horizon and may not be easily visible to the user
    public string? FcmToken { get; set; }// Optional Firebase Cloud Messaging (FCM) token for push notifications, which can be used to send notifications about upcoming passes to the user's device
    public DateTime UpdatedAt { get; set; } // Timestamp of when the settings were last updated
}