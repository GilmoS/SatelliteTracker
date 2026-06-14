namespace SatelliteTracker.PassService.SGP4;
// this record represents the result of a satellite pass over a ground station,
// including the satellite's ID, the times of acquisition and loss of signal,
// the maximum elevation, azimuths at AOS and LOS, and the duration of the pass in seconds
public sealed record PassResult(
    Guid SatelliteId,
    DateTime AOS,
    DateTime LOS,
    double MaxElevation,
    double AosAzimuth,
    double LosAzimuth,
    int DurationSeconds);
