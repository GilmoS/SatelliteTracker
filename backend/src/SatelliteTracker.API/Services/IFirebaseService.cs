namespace SatelliteTracker.API.Services;

public interface IFirebaseService
{
    Task SendPassNotificationAsync(string fcmToken, string satelliteName, DateTime aos, int minutesBefore);
}
