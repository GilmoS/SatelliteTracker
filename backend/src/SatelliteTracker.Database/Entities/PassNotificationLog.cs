namespace SatelliteTracker.Database.Entities;

// One row per (pass, tester, threshold) notification attempt — the row is claimed (Pending) before
// the FCM send and then records the real outcome, so a failed send is never logged as delivered.
// A single tester can have multiple rows for the same pass, one per AlertMinutes threshold
// (e.g. AlertMinutes = [5, 10, 30] can produce up to 3 rows for one pass).
public class PassNotificationLog
{
    public Guid Id { get; set; }
    public Guid PassId { get; set; }
    public Guid ApiKeyId { get; set; }
    public int AlertMinutes { get; set; }
    public PassNotificationStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset LastAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; } // Set only once a send succeeds
    public string? LastError { get; set; }

    public Pass Pass { get; set; } = null!;
    public ApiKey ApiKey { get; set; } = null!;
}

public enum PassNotificationStatus
{
    Pending,   // Claimed by a job tick; the send is in flight
    Sent,      // FCM accepted the message — final
    Failed,    // Last attempt failed; retried while attempts and time remain
    Abandoned  // Gave up: attempts exhausted or the device token is dead — final
}
