namespace SatelliteTracker.API.DTOs;

public record HealthDetailsDto(string Status, IReadOnlyList<HealthCheckDetailDto> Checks);

public record HealthCheckDetailDto(string Name, string Status, double DurationMs, string Reason);
