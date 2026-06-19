using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.API.DTOs;

public class SatelliteDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int NoradId { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }

    public static SatelliteDto From(Satellite s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        NoradId = s.NoradId,
        Description = s.Description,
        IsActive = s.IsActive,
        IsDefault = s.IsDefault,
        CreatedAt = s.CreatedAt
    };
}

public class CreateSatelliteRequest
{
    public string Name { get; set; } = string.Empty;
    public int NoradId { get; set; }
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
}

public class UpdateSatelliteRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsDefault { get; set; }
}
