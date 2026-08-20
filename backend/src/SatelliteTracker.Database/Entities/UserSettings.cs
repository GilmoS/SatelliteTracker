namespace SatelliteTracker.Database.Entities;

// Per-tester settings, keyed 1:1 to an ApiKey. Holds the notification preferences that used
// to live on the single global Settings row before the beta multi-tester model.
public class UserSettings
{
    public Guid Id { get; set; }
    public Guid ApiKeyId { get; set; }
    public string? FcmToken { get; set; }
    public int[] AlertMinutes { get; set; } = [5, 10, 30];
    public DateTimeOffset UpdatedAt { get; set; }

    public ApiKey ApiKey { get; set; } = null!;
}
