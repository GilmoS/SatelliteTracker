namespace SatelliteTracker.Database.Entities;

// Append-only ledger of notifications actually sent — one row per (pass, tester, threshold) that
// fired, not a flag. A single tester can have multiple rows for the same pass, one per
// AlertMinutes threshold (e.g. AlertMinutes = [5, 10, 30] can produce up to 3 rows for one pass).
public class PassNotificationLog
{
    public Guid Id { get; set; }
    public Guid PassId { get; set; }
    public Guid ApiKeyId { get; set; }
    public int AlertMinutes { get; set; }
    public DateTimeOffset SentAt { get; set; }

    public Pass Pass { get; set; } = null!;
    public ApiKey ApiKey { get; set; } = null!;
}
