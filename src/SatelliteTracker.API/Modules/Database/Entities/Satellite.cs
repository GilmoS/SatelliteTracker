namespace SatelliteTracker.API.Modules.Database.Entities;

public class Satellite
{
    public Guid Id { get; set; } // Primary key for the Satellite entity
    public string Name { get; set; } = string.Empty; // Name of the satellite, which can be used for display purposes
    public int NoradId { get; set; } // NORAD ID of the satellite, which is a unique identifier assigned by the North American Aerospace Defense Command
    public string? Description { get; set; } // Optional description of the satellite, which can provide additional information about the satellite's purpose, history, or other relevant details
    public bool IsActive { get; set; } // Indicates whether the satellite is currently active or not, which can be used to filter out inactive satellites from the list of satellites being tracked
    public bool IsDefault { get; set; } // Indicates whether this satellite is the default satellite for the user, which can be used to automatically select this satellite when the user accesses the application
    public DateTime CreatedAt { get; set; } // Timestamp of when the satellite record was created, which can be useful for auditing and tracking purposes

    public ICollection<TleRecord> TleRecords { get; set; } = []; // Collection of TLE records associated with this satellite, which can be used to track the satellite's orbit and predict its future positions
    public ICollection<Pass> Passes { get; set; } = [];// Collection of passes associated with this satellite, which can be used to track the satellite's visibility and predict when it will be visible from a specific location
}