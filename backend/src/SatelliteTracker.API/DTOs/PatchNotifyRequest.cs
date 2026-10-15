namespace SatelliteTracker.API.DTOs;

public record PatchNotifyRequest(bool Notify);

public record NotifyStatusDto(Guid PassId, bool Notify);
