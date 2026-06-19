namespace SatelliteTracker.Database.Entities;

//This class represents a satellite, which is an object in space that orbits the Earth.
//It contains information about the satellite, such as its name, NORAD ID, description, and status.
//This allows tracking and managing satellites for observation or other purposes.
public class Satellite
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int NoradId { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } // Indicates whether the satellite is currently active or not
    public bool IsDefault { get; set; } // Indicates whether this satellite is the default one for new passes (useful for user experience)
    public DateTime CreatedAt { get; set; }

    public ICollection<TleRecord> TleRecords { get; set; } = [];// Navigation property to the collection of associated TleRecord entities
    public ICollection<Pass> Passes { get; set; } = [];// Navigation property to the collection of associated Pass entities
}
