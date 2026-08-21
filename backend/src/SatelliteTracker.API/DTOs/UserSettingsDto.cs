using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.API.DTOs;

public class UserSettingsDto
{
    public string? FcmToken { get; set; }
    public int[] AlertMinutes { get; set; } = [];

    public static UserSettingsDto From(UserSettings s) => new()
    {
        FcmToken = s.FcmToken,
        AlertMinutes = s.AlertMinutes
    };

    // Computed default returned by GET /api/settings/me when no UserSettings row exists yet —
    // never written to the DB, see SettingsMeController.
    public static UserSettingsDto Default => new() { FcmToken = null, AlertMinutes = [] };
}

public record UpdateAlertMinutesRequest(int[] AlertMinutes);
public record UpdateFcmTokenRequest(string FcmToken);
