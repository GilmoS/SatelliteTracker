namespace SatelliteTracker.Database.Entities;
//This class represents a TLE (Two-Line Element) record, which contains the orbital parameters of a satellite at a specific epoch.
public class TleRecord
{
    public Guid Id { get; set; } // Primary key
    public Guid SatelliteId { get; set; } // Foreign key to the Satellite entity
    public string Line1 { get; set; } = string.Empty; // First line of the TLE data
    public string Line2 { get; set; } = string.Empty; // Second line of the TLE data
    public DateTime Epoch { get; set; } // Epoch time of the TLE data
    public DateTime FetchedAt { get; set; } // Timestamp for when the TLE data was fetched

    public Satellite Satellite { get; set; } = null!; // Navigation property to the Satellite entity
    public ICollection<Pass> Passes { get; set; } = [];// Navigation property to the collection of associated Pass entities
}
