namespace SatelliteTracker.Database.Entities;

// Per-tester notification opt-out for a specific pass. Sparse table: a row exists ONLY once a
// tester has actively toggled notifications off for this pass — there is no row created by
// default when a pass is calculated or when a tester registers. Absence of a row means
// Notify = true (opt-out model). Callers must LEFT JOIN + COALESCE to true, never assume a row
// exists — see IPassSubscriptionRepository.GetEffectiveNotifyStatusAsync.
public class PassSubscription
{
    public Guid Id { get; set; }
    public Guid PassId { get; set; }
    public Guid ApiKeyId { get; set; }
    public bool Notify { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Pass Pass { get; set; } = null!;
    public ApiKey ApiKey { get; set; } = null!;
}
