namespace SatelliteTracker.Database.Entities;

public class Note
{
    public Guid Id { get; set; }
    public Guid PassId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Pass Pass { get; set; } = null!;
}
