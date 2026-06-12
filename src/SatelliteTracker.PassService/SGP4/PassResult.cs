namespace SatelliteTracker.PassService.SGP4;

public sealed record PassResult(
    Guid SatelliteId,
    DateTime AOS,
    DateTime LOS,
    double MaxElevation,
    double AosAzimuth,
    double LosAzimuth,
    int DurationSeconds);
