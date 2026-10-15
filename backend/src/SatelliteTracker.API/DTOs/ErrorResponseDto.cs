namespace SatelliteTracker.API.DTOs;

// Shared shape for every error response in the API (see BaseController.ToError and the uniform
// 401 body written directly by ApiKeyAuthenticationHandler) — exists so ProducesResponseType
// attributes across controllers have one real type to point at instead of an anonymous object,
// which lets the OpenAPI spec (and therefore the Android-generated DTOs) describe error bodies.
public record ErrorResponseDto(string Error);
