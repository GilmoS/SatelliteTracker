namespace SatelliteTracker.Database.Entities;

// Represents a beta tester's registered API key. The raw key is shown to the user
// exactly once at registration and is never stored — only its SHA-256 hash lives here.
public class ApiKey
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }

    public UserSettings? UserSettings { get; set; }
    public ICollection<PassSubscription> PassSubscriptions { get; set; } = [];
    public ICollection<PassNotificationLog> PassNotificationLogs { get; set; } = [];
}
