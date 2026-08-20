namespace SatelliteTracker.Database.Entities;

// One row per email an admin has approved for beta self-registration (see
// SatelliteTracker.API AdminController / AuthController). Email is always stored
// trimmed and lowercased — every read/write must normalize the same way, since
// uniqueness is enforced by that normalization plus a unique index, not DB collation.
public class AllowlistedEmail
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset AddedAt { get; set; }
}
