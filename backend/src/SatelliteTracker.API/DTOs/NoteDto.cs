using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.API.DTOs;

public class NoteDto
{
    public Guid Id { get; set; }
    public Guid PassId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public static NoteDto From(Note n) => new()
    {
        Id = n.Id,
        PassId = n.PassId,
        Content = n.Content,
        CreatedAt = n.CreatedAt,
        UpdatedAt = n.UpdatedAt
    };
}

public class CreateNoteRequest
{
    public string Content { get; set; } = string.Empty;
}

public class UpdateNoteRequest
{
    public string Content { get; set; } = string.Empty;
}
