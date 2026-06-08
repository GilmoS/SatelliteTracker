namespace SatelliteTracker.API.Modules.Database.Entities;

public class Note
{
    public Guid Id { get; set; } // Primary key for the Note entity
    public Guid PassId { get; set; } // Foreign key referencing the related Pass
    public string Content { get; set; } = string.Empty; // Content of the note, which can be used to store any relevant information or observations about the pass
    public DateTime CreatedAt { get; set; } // Timestamp of when the note was created
    public DateTime UpdatedAt { get; set; } // Timestamp of when the note was last updated

    public Pass Pass { get; set; } = null!; // Navigation property to the related Pass, which allows for easy access to the pass's information when working with notes
}