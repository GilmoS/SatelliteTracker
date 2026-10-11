namespace SatelliteTracker.API.Services;

public interface IFirebaseService
{
    /// <summary>
    /// Sends one pass reminder. Never throws — every failure is reported in the returned
    /// <see cref="FcmSendResult"/> so the caller can record and retry it.
    /// </summary>
    Task<FcmSendResult> SendPassNotificationAsync(string fcmToken, string satelliteName, DateTime aos, int minutesBefore, Guid passId);
}

public enum FcmSendOutcome
{
    Sent,
    Failed,       // Possibly transient (FCM outage, quota, Firebase not configured) — worth retrying
    TokenInvalid  // FCM says the device token is dead — retrying with it can never succeed
}

public sealed record FcmSendResult(FcmSendOutcome Outcome, string? Error = null)
{
    public static readonly FcmSendResult Sent = new(FcmSendOutcome.Sent);
    public static FcmSendResult Failed(string error) => new(FcmSendOutcome.Failed, error);
    public static FcmSendResult TokenInvalid(string error) => new(FcmSendOutcome.TokenInvalid, error);
}
