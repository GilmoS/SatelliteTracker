namespace SatelliteTracker.API.DTOs;

public record RegisterRequest(string Email, string DisplayName);

// ApiKey is the raw key — visible exactly once, in this response, and never stored.
public record RegisterResponse(string ApiKey, string Email, string DisplayName);
