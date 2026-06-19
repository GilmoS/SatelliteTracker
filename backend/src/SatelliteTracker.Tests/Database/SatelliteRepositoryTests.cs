using Microsoft.Data.Sqlite;
using SatelliteTracker.Database;
using Xunit;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.Tests.Database.Helpers;

namespace SatelliteTracker.Tests.Database;

public class SatelliteRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly SatelliteRepository _repo;

    public SatelliteRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _repo = new SatelliteRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static Satellite MakeSatellite(int noradId = 12345, bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"SAT-{noradId}",
        NoradId = noradId,
        IsActive = isActive,
        IsDefault = false,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GetAllAsync_ReturnsSatellites()
    {
        _context.Satellites.Add(MakeSatellite(11111));
        _context.Satellites.Add(MakeSatellite(22222));
        await _context.SaveChangesAsync();

        var result = await _repo.GetAllAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count());
    }

    [Fact]
    public async Task GetAllAsync_EmptyDatabase_ReturnsEmptyList()
    {
        var result = await _repo.GetAllAsync();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsSatellite()
    {
        var sat = MakeSatellite(55555);
        _context.Satellites.Add(sat);
        await _context.SaveChangesAsync();

        var result = await _repo.GetByIdAsync(sat.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(55555, result.Value!.NoradId);
    }

    [Fact]
    public async Task GetByIdAsync_MissingId_ReturnsFailure()
    {
        var result = await _repo.GetByIdAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task GetByNoradIdAsync_ExistingNoradId_ReturnsSatellite()
    {
        _context.Satellites.Add(MakeSatellite(99999));
        await _context.SaveChangesAsync();

        var result = await _repo.GetByNoradIdAsync(99999);

        Assert.True(result.IsSuccess);
        Assert.Equal(99999, result.Value!.NoradId);
    }

    [Fact]
    public async Task GetByNoradIdAsync_MissingNoradId_ReturnsFailure()
    {
        var result = await _repo.GetByNoradIdAsync(99999);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsOnlyActiveSatellites()
    {
        _context.Satellites.Add(MakeSatellite(11111, isActive: true));
        _context.Satellites.Add(MakeSatellite(22222, isActive: false));
        await _context.SaveChangesAsync();

        var result = await _repo.GetActiveAsync();

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal(11111, result.Value!.First().NoradId);
    }

    [Fact]
    public async Task AddAsync_PersistsSatellite()
    {
        var sat = MakeSatellite(77777);

        var result = await _repo.AddAsync(sat);

        Assert.True(result.IsSuccess);
        Assert.Equal(77777, result.Value!.NoradId);
        Assert.Equal(1, _context.Satellites.Count());
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var sat = MakeSatellite(88888);
        _context.Satellites.Add(sat);
        await _context.SaveChangesAsync();

        sat.Name = "Updated Name";
        var result = await _repo.UpdateAsync(sat);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Name", result.Value!.Name);
    }

    [Fact]
    public async Task UpdateAsync_IsActive_TogglesPersists()
    {
        var sat = MakeSatellite(44444, isActive: true);
        _context.Satellites.Add(sat);
        await _context.SaveChangesAsync();

        sat.IsActive = false;
        await _repo.UpdateAsync(sat);

        var fetched = await _repo.GetByNoradIdAsync(44444);
        Assert.True(fetched.IsSuccess);
        Assert.False(fetched.Value!.IsActive);
    }
}
