using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;

namespace SatelliteTracker.API.Services;

public class FirebaseService : IFirebaseService
{
    private readonly ILogger<FirebaseService> _logger;

    public FirebaseService(IConfiguration configuration, ILogger<FirebaseService> logger)
    {
        _logger = logger;

        if (FirebaseApp.DefaultInstance is not null) return;

        var path = configuration["Firebase:ServiceAccountPath"];
        if (string.IsNullOrEmpty(path))
        {
            _logger.LogWarning("Firebase:ServiceAccountPath not configured — push notifications disabled");
            return;
        }

        try
        {
            using var stream = File.OpenRead(path);
#pragma warning disable CS0618
            var credential = GoogleCredential.FromStream(stream);
#pragma warning restore CS0618
            FirebaseApp.Create(new AppOptions { Credential = credential });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Firebase — push notifications disabled");
        }
    }

    public async Task SendPassNotificationAsync(string fcmToken, string satelliteName, DateTime aos, int minutesBefore)
    {
        if (FirebaseApp.DefaultInstance is null)
        {
            _logger.LogWarning("Firebase not initialized — skipping notification for {Satellite}", satelliteName);
            return;
        }

        try
        {
            var message = new Message
            {
                Token = fcmToken,
                Notification = new Notification
                {
                    Title = $"חליפה מתקרבת — {satelliteName}",
                    Body = $"חליפה תתחיל בעוד {minutesBefore} דקות ({aos:HH:mm} UTC)"
                }
            };

            await FirebaseMessaging.DefaultInstance.SendAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send FCM notification for satellite {Satellite}", satelliteName);
        }
    }
}
