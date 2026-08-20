using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SatelliteTracker.Database;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.Tests.Database.Helpers;
using Xunit;

namespace SatelliteTracker.Tests.Database;

public class AllowlistedEmailRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly AllowlistedEmailRepository _repo;

    public AllowlistedEmailRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _repo = new AllowlistedEmailRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task AddAsync_NewEmail_Inserts()
    {
        var result = await _repo.AddAsync("tester@iai.co.il");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _context.AllowlistedEmails.Count());
    }

    [Fact]
    public async Task AddAsync_DuplicateNormalizedEmail_IsIdempotent()
    {
        await _repo.AddAsync("tester@iai.co.il");
        var result = await _repo.AddAsync("tester@iai.co.il");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _context.AllowlistedEmails.Count());
    }

    [Fact]
    public async Task ExistsAsync_MatchingNormalizedEmail_ReturnsTrue()
    {
        await _repo.AddAsync("tester@iai.co.il");

        var result = await _repo.ExistsAsync("tester@iai.co.il");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    [Fact]
    public async Task ExistsAsync_NoMatch_ReturnsFalse()
    {
        var result = await _repo.ExistsAsync("nobody@iai.co.il");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllRows()
    {
        await _repo.AddAsync("a@iai.co.il");
        await _repo.AddAsync("b@iai.co.il");

        var result = await _repo.GetAllAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count());
    }

    // Confirms the DB-level unique index, independent of the repository's own idempotency
    // check — mirrors AppDbContextConfigurationTests' pattern for the UserSettings 1:1 index.
    [Fact]
    public async Task Email_UniqueIndex_PreventsDuplicateNormalizedEmail()
    {
        _context.AllowlistedEmails.Add(new AllowlistedEmail
        {
            Id = Guid.NewGuid(),
            Email = "tester@iai.co.il",
            AddedAt = DateTimeOffset.UtcNow
        });
        await _context.SaveChangesAsync();

        using var secondContext = TestDbContextFactory.CreateAdditionalContext(_connection);
        secondContext.AllowlistedEmails.Add(new AllowlistedEmail
        {
            Id = Guid.NewGuid(),
            Email = "tester@iai.co.il",
            AddedAt = DateTimeOffset.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => secondContext.SaveChangesAsync());
    }
}
