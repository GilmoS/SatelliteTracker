using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SatelliteTracker.Database;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Tests.Database.Helpers;
using Xunit;

namespace SatelliteTracker.Tests.Database;

// Verifies the EF Fluent API configuration that a navigation property alone can't enforce.
public class AppDbContextConfigurationTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;

    public AppDbContextConfigurationTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static ApiKey MakeApiKey(string email = "tester@iai.co.il") => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        DisplayName = "Tester",
        KeyHash = new string('a', 64),
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task UserSettings_ApiKeyIdUniqueIndex_PreventsSecondRowForSameApiKey()
    {
        var apiKey = MakeApiKey();
        _context.ApiKeys.Add(apiKey);
        await _context.SaveChangesAsync();

        _context.UserSettings.Add(new UserSettings
        {
            Id = Guid.NewGuid(),
            ApiKeyId = apiKey.Id,
            AlertMinutes = [5, 10],
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _context.SaveChangesAsync();

        // A fresh context is required here: reusing _context would let EF's own relationship
        // fixup for the 1:1 navigation quietly delete-then-replace the first row instead of
        // ever sending a conflicting INSERT to the database.
        using var secondContext = TestDbContextFactory.CreateAdditionalContext(_connection);
        secondContext.UserSettings.Add(new UserSettings
        {
            Id = Guid.NewGuid(),
            ApiKeyId = apiKey.Id,
            AlertMinutes = [15],
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => secondContext.SaveChangesAsync());
    }

    [Fact]
    public async Task UserSettings_DifferentApiKeyId_AllowsSeparateRows()
    {
        var apiKey1 = MakeApiKey("tester1@iai.co.il");
        var apiKey2 = MakeApiKey("tester2@iai.co.il");
        _context.ApiKeys.AddRange(apiKey1, apiKey2);
        await _context.SaveChangesAsync();

        _context.UserSettings.AddRange(
            new UserSettings { Id = Guid.NewGuid(), ApiKeyId = apiKey1.Id, AlertMinutes = [5], UpdatedAt = DateTimeOffset.UtcNow },
            new UserSettings { Id = Guid.NewGuid(), ApiKeyId = apiKey2.Id, AlertMinutes = [5], UpdatedAt = DateTimeOffset.UtcNow }
        );

        await _context.SaveChangesAsync();

        Assert.Equal(2, _context.UserSettings.Count());
    }
}
