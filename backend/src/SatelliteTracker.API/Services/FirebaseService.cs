using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;

namespace SatelliteTracker.API.Services;

// Sends FCM messages through the default FirebaseApp, which FirebaseAppInitializer creates at
// startup from the validated Firebase:ServiceAccountJson. The host does not start without it.
public class FirebaseService : IFirebaseService
{
    private readonly ILogger<FirebaseService> _logger;

    public FirebaseService(ILogger<FirebaseService> logger)
    {
        _logger = logger;
    }

    // FCM error codes that mean the token itself is unusable, so a retry can never succeed.
    private static readonly HashSet<MessagingErrorCode> DeadTokenCodes =
    [
        MessagingErrorCode.Unregistered,
        MessagingErrorCode.SenderIdMismatch,
        MessagingErrorCode.InvalidArgument
    ];

    public async Task<FcmSendResult> SendPassNotificationAsync(string fcmToken, string satelliteName, DateTime aos, int minutesBefore, Guid passId)
    {
        try
        {
            var message = new Message
            {
                Token = fcmToken,
                Notification = new Notification
                {
                    Title = $"Upcoming pass for {satelliteName}",
                    Body = $"The pass will start in {minutesBefore} minutes ({aos:HH:mm} UTC)"
                },
                Data = new Dictionary<string, string>
                {
                    { "passId", passId.ToString() },
                    { "type", "pass_reminder" }
                }
            };

            await FirebaseMessaging.DefaultInstance.SendAsync(message);
            return FcmSendResult.Sent;
        }
        catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode is { } code && DeadTokenCodes.Contains(code))
        {
            _logger.LogWarning(ex, "FCM rejected the device token ({Code}) for satellite {Satellite}", code, satelliteName);
            return FcmSendResult.TokenInvalid($"{code}: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send FCM notification for satellite {Satellite}", satelliteName);
            return FcmSendResult.Failed(ex.Message);
        }
    }
}
