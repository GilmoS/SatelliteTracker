namespace SatelliteTracker.API.HealthChecks;

// Obtains an access token from the Firebase credential; throws if it can't. An abstraction so the
// FCM health check can be tested without reaching Google.
public interface IFcmTokenProbe
{
    Task RequestTokenAsync(CancellationToken ct);
}
