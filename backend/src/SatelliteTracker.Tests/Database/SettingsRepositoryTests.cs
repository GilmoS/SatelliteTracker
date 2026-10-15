using Microsoft.Data.Sqlite;
using SatelliteTracker.Database;
using Xunit;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.Tests.Database.Helpers;

namespace SatelliteTracker.Tests.Database;

public class SettingsRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly SettingsRepository _repo;

    public SettingsRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _repo = new SettingsRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static Settings MakeSettings() => new()
    {
        Id = Guid.NewGuid(),
        OutlookDays = 7,
        TeamEmail = "team@iai.co.il",
        MinElevation = 10,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GetAsync_NoSettings_ReturnsFailure()
    {
        var result = await _repo.GetAsync();

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task GetAsync_SettingsExist_ReturnsSettings()
    {
        _context.Settings.Add(MakeSettings());
        await _context.SaveChangesAsync();

        var result = await _repo.GetAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value!.OutlookDays);
    }

    [Fact]
    public async Task UpsertAsync_NoExisting_InsertsSettings()
    {
        var settings = MakeSettings();

        var result = await _repo.UpsertAsync(settings);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _context.Settings.Count());
    }

    [Fact]
    public async Task UpsertAsync_ExistingSettings_UpdatesInPlace()
    {
        _context.Settings.Add(MakeSettings());
        await _context.SaveChangesAsync();

        var updated = new Settings
        {
            OutlookDays = 14,
            TeamEmail = "new@iai.co.il",
            MinElevation = 20,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _repo.UpsertAsync(updated);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _context.Settings.Count());
        Assert.Equal(14, result.Value!.OutlookDays);
        Assert.Equal("new@iai.co.il", result.Value!.TeamEmail);
    }

    [Fact]
    public async Task UpsertAsync_TwiceSameRow_OnlyOneRowExists()
    {
        var s1 = MakeSettings();
        await _repo.UpsertAsync(s1);

        var s2 = MakeSettings();
        s2.OutlookDays = 30;
        await _repo.UpsertAsync(s2);

        Assert.Equal(1, _context.Settings.Count());
    }
}
