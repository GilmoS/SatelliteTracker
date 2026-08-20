using Microsoft.Data.Sqlite;
using SatelliteTracker.Database;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.Tests.Database.Helpers;
using Xunit;

namespace SatelliteTracker.Tests.Database;

public class ApiKeyRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly ApiKeyRepository _repo;

    public ApiKeyRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _repo = new ApiKeyRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static ApiKey MakeApiKey(bool isActive, string email = "tester@iai.co.il") => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        DisplayName = "Tester",
        KeyHash = new string('a', 64),
        IsActive = isActive,
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task GetActiveByEmailAsync_ActiveKeyExists_ReturnsIt()
    {
        var key = MakeApiKey(isActive: true);
        _context.ApiKeys.Add(key);
        await _context.SaveChangesAsync();

        var result = await _repo.GetActiveByEmailAsync("tester@iai.co.il");

        Assert.True(result.IsSuccess);
        Assert.Equal(key.Id, result.Value!.Id);
    }

    [Fact]
    public async Task GetActiveByEmailAsync_OnlyRevokedKeyExists_ReturnsFailure()
    {
        var key = MakeApiKey(isActive: false);
        _context.ApiKeys.Add(key);
        await _context.SaveChangesAsync();

        var result = await _repo.GetActiveByEmailAsync("tester@iai.co.il");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetActiveByEmailAsync_NoKeyExists_ReturnsFailure()
    {
        var result = await _repo.GetActiveByEmailAsync("nobody@iai.co.il");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task CreateAsync_InsertsRow()
    {
        var key = MakeApiKey(isActive: true);

        var result = await _repo.CreateAsync(key);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _context.ApiKeys.Count());
    }
}
