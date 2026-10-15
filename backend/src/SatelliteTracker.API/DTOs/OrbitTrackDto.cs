using SatelliteTracker.PassService.SGP4;

namespace SatelliteTracker.API.DTOs;

// Response of GET api/satellites/{id}/orbit: the SGP4-computed ground track from one orbital
// period before "now" to one period after it, from the satellite's latest TLE. Points use the same
// TrackPointDto shape (Unix-seconds timestamps) as the live N2YO track.
public class OrbitTrackDto
{
    public Guid SatelliteId { get; set; }
    public int NoradId { get; set; }
    public double PeriodMinutes { get; set; }
    public List<TrackPointDto> Points { get; set; } = [];

    public static OrbitTrackDto From(Guid satelliteId, OrbitTrack track) => new()
    {
        SatelliteId = satelliteId,
        NoradId = track.NoradId,
        PeriodMinutes = track.PeriodMinutes,
        Points = track.Points.Select(TrackPointDto.From).ToList()
    };
}
