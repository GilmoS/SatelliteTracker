namespace SatelliteTracker.API.Modules.Database.Entities;

public class TleRecord
{
    public Guid Id { get; set; } // Primary key for the TleRecord entity
    public Guid SatelliteId { get; set; } // Foreign key referencing the related Satellite
    public string Line1 { get; set; } = string.Empty; // First line of the TLE data, which contains the satellite's catalog number, classification, and other metadata
    public string Line2 { get; set; } = string.Empty; // Second line of the TLE data, which contains the satellite's orbital parameters such as inclination, right ascension, eccentricity, argument of perigee, mean anomaly, and mean motion
    public DateTime Epoch { get; set; }// Epoch time of the TLE data, which represents the time at which the orbital parameters are valid and can be used to calculate the satellite's position at any given time
    public DateTime FetchedAt { get; set; }// Timestamp of when the TLE data was fetched, which can be useful for tracking the freshness of the data and determining when to fetch new TLE data for the satellite

    public Satellite Satellite { get; set; } = null!; // Navigation property to the related Satellite, which allows for easy access to the satellite's information when working with TLE records
    public ICollection<Pass> Passes { get; set; } = [];// Collection of passes associated with this TLE record, which can be used to track the satellite's visibility and predict when it will be visible from a specific location based on the orbital parameters contained in the TLE data
}