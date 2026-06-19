using Microsoft.EntityFrameworkCore;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;
// This class implements the INoteRepository interface, providing methods to manage Note entities in the database.
public class NoteRepository : INoteRepository
{
    private readonly AppDbContext _context; // The database context used to interact with the Notes table in the database.


    public NoteRepository(AppDbContext context) => _context = context; // Constructor that initializes the repository with the database context.


    // Retrieves all notes associated with a specific pass ID, ordered by creation date.
    public async Task<Result<IEnumerable<Note>>> GetByPassIdAsync(Guid passId)
    {
        try
        {
            var notes = await _context.Notes
                .Where(n => n.PassId == passId)
                .OrderBy(n => n.CreatedAt)
                .ToListAsync();

            return Result<IEnumerable<Note>>.Success(notes); // Returns a successful result containing the list of notes.
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<Note>>.Failure(ex.Message); // Returns a failure result with the error message.
        }
    }

    // Retrieves a note by its unique identifier.
    public async Task<Result<Note>> GetByIdAsync(Guid id)
    {
        try
        {
            var note = await _context.Notes.FindAsync(id);
            // Checks if the note was found and returns either a success or failure result accordingly.
            return note is null? Result<Note>.Failure($"Note {id} not found."): Result<Note>.Success(note);
        }
        catch (Exception ex)
        {
            return Result<Note>.Failure(ex.Message);
        }
    }

    // Adds a new note to the database and returns the result of the operation.
    public async Task<Result<Note>> AddAsync(Note note)
    {
        try
        {
            _context.Notes.Add(note);
            await _context.SaveChangesAsync(); // Saves the changes to the database and returns a successful result containing the added note.
            return Result<Note>.Success(note); // Returns a successful result containing the added note.
        }
        catch (Exception ex)
        {
            return Result<Note>.Failure(ex.Message); // Returns a failure result with the error message if an exception occurs during the add operation.
        }
    }

    // Updates an existing note in the database and returns the result of the operation.
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

    // Deletes a note from the database by its ID and returns the result of the operation.
    public async Task<Result<bool>> DeleteAsync(Guid id)
    {
        try
        {
            var note = await _context.Notes.FindAsync(id);
            if (note is null)
                return Result<bool>.Failure($"Note {id} not found."); // Returns a failure result if the note to be deleted is not found.

            _context.Notes.Remove(note);
            await _context.SaveChangesAsync();
            return Result<bool>.Success(true); /// Returns a successful result indicating that the note was successfully deleted.
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(ex.Message); // Returns a failure result with the error message if an exception occurs during the delete operation.
        }
    }
}
