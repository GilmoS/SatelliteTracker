using Microsoft.Data.Sqlite;
using SatelliteTracker.Database;
using Xunit;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.Tests.Database.Helpers;

namespace SatelliteTracker.Tests.Database;

public class NoteRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly NoteRepository _repo;

    public NoteRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _repo = new NoteRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private Pass SeedPass()
    {
        var sat = new Satellite
        {
            Id = Guid.NewGuid(), Name = "SAT", NoradId = 11111, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var tle = new TleRecord
        {
            Id = Guid.NewGuid(), SatelliteId = sat.Id,
            Line1 = "1 11111U 98067A   23001.00000000  .00000000  00000-0  00000-0 0  9999",
            Line2 = "2 11111  51.6400 208.9163 0001382  95.6617 344.7248 15.49309522223145",
            Epoch = DateTime.UtcNow, FetchedAt = DateTime.UtcNow
        };
        var pass = new Pass
        {
            Id = Guid.NewGuid(), SatelliteId = sat.Id, TleId = tle.Id,
            Aos = DateTime.UtcNow.AddHours(1), Los = DateTime.UtcNow.AddHours(2),
            MaxElevation = 45, AosAzimuth = 90, LosAzimuth = 270,
            DurationSec = 360, CalculatedAt = DateTime.UtcNow
        };
        _context.Satellites.Add(sat);
        _context.TleRecords.Add(tle);
        _context.Passes.Add(pass);
        _context.SaveChanges();
        return pass;
    }

    private Note MakeNote(Guid passId, string content = "Test note") => new()
    {
        Id = Guid.NewGuid(),
        PassId = passId,
        Content = content,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GetByPassIdAsync_ReturnsNotesForPass()
    {
        var pass = SeedPass();
        _context.Notes.Add(MakeNote(pass.Id, "Note A"));
        _context.Notes.Add(MakeNote(pass.Id, "Note B"));
        await _context.SaveChangesAsync();

        var result = await _repo.GetByPassIdAsync(pass.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count());
    }

    [Fact]
    public async Task GetByPassIdAsync_UnknownPass_ReturnsEmptyList()
    {
        var result = await _repo.GetByPassIdAsync(Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task AddAsync_PersistsNote()
    {
        var pass = SeedPass();
        var note = MakeNote(pass.Id);

        var result = await _repo.AddAsync(note);

        Assert.True(result.IsSuccess);
        Assert.Equal(note.Content, result.Value!.Content);
        Assert.Equal(1, _context.Notes.Count());
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var pass = SeedPass();
        var note = MakeNote(pass.Id, "Original");
        _context.Notes.Add(note);
        await _context.SaveChangesAsync();

        note.Content = "Updated";
        var result = await _repo.UpdateAsync(note);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated", result.Value!.Content);
    }

    [Fact]
    public async Task DeleteAsync_RemovesNote()
    {
        var pass = SeedPass();
        var note = MakeNote(pass.Id);
        _context.Notes.Add(note);
        await _context.SaveChangesAsync();

        var result = await _repo.DeleteAsync(note.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, _context.Notes.Count());
    }

    [Fact]
    public async Task DeleteAsync_MissingId_ReturnsFailure()
    {
        var result = await _repo.DeleteAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task DeletePass_CascadeDeletesNotes()
    {
        var pass = SeedPass();
        _context.Notes.Add(MakeNote(pass.Id));
        await _context.SaveChangesAsync();

        _context.Passes.Remove(pass);
        await _context.SaveChangesAsync();

        Assert.Equal(0, _context.Notes.Count());
    }
}
