using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.API.DTOs;

public class SettingsDto
{
    public Guid Id { get; set; }
    public int OutlookDays { get; set; }
    public string? TeamEmail { get; set; }
    public decimal MinElevation { get; set; }
    public DateTime UpdatedAt { get; set; }

    public static SettingsDto From(Settings s) => new()
    {
        Id = s.Id,
        OutlookDays = s.OutlookDays,
        TeamEmail = s.TeamEmail,
        MinElevation = s.MinElevation,
        UpdatedAt = s.UpdatedAt
    };
}

public class UpdateSettingsRequest
{
    public int? OutlookDays { get; set; }
    public string? TeamEmail { get; set; }
    public decimal? MinElevation { get; set; }
}
