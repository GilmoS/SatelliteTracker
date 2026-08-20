using Microsoft.Data.Sqlite;
using SatelliteTracker.Database;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.Tests.Database.Helpers;
using Xunit;

namespace SatelliteTracker.Tests.Database;

public class UserSettingsRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly UserSettingsRepository _repo;

    public UserSettingsRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _repo = new UserSettingsRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static ApiKey MakeApiKey(bool isActive = true, string email = "tester@iai.co.il") => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        DisplayName = "Tester",
        KeyHash = new string('a', 64),
        IsActive = isActive,
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task GetByApiKeyIdAsync_NoSettings_ReturnsFailure()
    {
        var result = await _repo.GetByApiKeyIdAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task GetByApiKeyIdAsync_SettingsExist_ReturnsSettings()
    {
        var apiKey = MakeApiKey();
        _context.ApiKeys.Add(apiKey);
        await _context.SaveChangesAsync();

        _context.UserSettings.Add(new UserSettings
        {
            Id = Guid.NewGuid(),
            ApiKeyId = apiKey.Id,
            FcmToken = "token-1",
            AlertMinutes = [5, 10],
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _context.SaveChangesAsync();

        var result = await _repo.GetByApiKeyIdAsync(apiKey.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("token-1", result.Value!.FcmToken);
    }

    [Fact]
    public async Task UpsertAsync_NoExisting_InsertsSettings()
    {
        var apiKey = MakeApiKey();
        _context.ApiKeys.Add(apiKey);
        await _context.SaveChangesAsync();

        var result = await _repo.UpsertAsync(new UserSettings
        {
            ApiKeyId = apiKey.Id,
            AlertMinutes = [5, 10, 30],
            UpdatedAt = DateTimeOffset.UtcNow
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _context.UserSettings.Count());
    }

    [Fact]
    public async Task UpsertAsync_ExistingSettings_UpdatesInPlace()
    {
        var apiKey = MakeApiKey();
        _context.ApiKeys.Add(apiKey);
        await _context.SaveChangesAsync();

        await _repo.UpsertAsync(new UserSettings
        {
            ApiKeyId = apiKey.Id,
            FcmToken = "old-token",
            AlertMinutes = [5],
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var result = await _repo.UpsertAsync(new UserSettings
        {
            ApiKeyId = apiKey.Id,
            FcmToken = "new-token",
            AlertMinutes = [10, 20],
            UpdatedAt = DateTimeOffset.UtcNow
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _context.UserSettings.Count());
        Assert.Equal("new-token", result.Value!.FcmToken);
    }

    [Fact]
    public async Task GetAllActiveAsync_OnlyReturnsSettingsForActiveApiKeys()
    {
        var activeKey = MakeApiKey(isActive: true, email: "active@iai.co.il");
        var revokedKey = MakeApiKey(isActive: false, email: "revoked@iai.co.il");
        _context.ApiKeys.AddRange(activeKey, revokedKey);
        await _context.SaveChangesAsync();

        _context.UserSettings.AddRange(
            new UserSettings { Id = Guid.NewGuid(), ApiKeyId = activeKey.Id, FcmToken = "active-token", AlertMinutes = [5], UpdatedAt = DateTimeOffset.UtcNow },
            new UserSettings { Id = Guid.NewGuid(), ApiKeyId = revokedKey.Id, FcmToken = "revoked-token", AlertMinutes = [5], UpdatedAt = DateTimeOffset.UtcNow }
        );
        await _context.SaveChangesAsync();

        var result = await _repo.GetAllActiveAsync();

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("active-token", result.Value!.First().FcmToken);
    }
}
