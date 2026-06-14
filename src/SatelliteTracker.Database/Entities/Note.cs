namespace SatelliteTracker.Database.Entities;

//This class represents a note associated with a satellite pass.
//It contains the content of the note, timestamps for creation and updates, and a reference to the associated pass.
//This allows users to add comments or observations about specific satellite passes.
public class Note
{
    public Guid Id { get; set; }
    public Guid PassId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Pass Pass { get; set; } = null!;
}
