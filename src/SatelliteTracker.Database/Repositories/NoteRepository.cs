using Microsoft.EntityFrameworkCore;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

public class NoteRepository : INoteRepository
{
    private readonly AppDbContext _context;

    public NoteRepository(AppDbContext context) => _context = context;

    public async Task<Result<IEnumerable<Note>>> GetByPassIdAsync(Guid passId)
    {
        try
        {
            var notes = await _context.Notes
                .Where(n => n.PassId == passId)
                .OrderBy(n => n.CreatedAt)
                .ToListAsync();

            return Result<IEnumerable<Note>>.Success(notes);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<Note>>.Failure(ex.Message);
        }
    }

    public async Task<Result<Note>> GetByIdAsync(Guid id)
    {
        try
        {
            var note = await _context.Notes.FindAsync(id);
            return note is null
                ? Result<Note>.Failure($"Note {id} not found.")
                : Result<Note>.Success(note);
        }
        catch (Exception ex)
        {
            return Result<Note>.Failure(ex.Message);
        }
    }

    public async Task<Result<Note>> AddAsync(Note note)
    {
        try
        {
            _context.Notes.Add(note);
            await _context.SaveChangesAsync();
            return Result<Note>.Success(note);
        }
        catch (Exception ex)
        {
            return Result<Note>.Failure(ex.Message);
        }
    }

    public async Task<Result<Note>> UpdateAsync(Note note)
    {
        try
        {
            _context.Notes.Update(note);
            await _context.SaveChangesAsync();
            return Result<Note>.Success(note);
        }
        catch (Exception ex)
        {
            return Result<Note>.Failure(ex.Message);
        }
    }

    public async Task<Result<bool>> DeleteAsync(Guid id)
    {
        try
        {
            var note = await _context.Notes.FindAsync(id);
            if (note is null)
                return Result<bool>.Failure($"Note {id} not found.");

            _context.Notes.Remove(note);
            await _context.SaveChangesAsync();
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(ex.Message);
        }
    }
}
