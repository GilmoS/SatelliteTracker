using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.API.DTOs;

public class AllowlistedEmailDto
{
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset AddedAt { get; set; }

    public static AllowlistedEmailDto From(AllowlistedEmail e) => new()
    {
        Email = e.Email,
        AddedAt = e.AddedAt
    };
}

public record AddAllowlistEmailRequest(string Email);

public record ReissueRequest(string Email);
