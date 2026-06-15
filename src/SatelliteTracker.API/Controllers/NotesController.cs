using Microsoft.AspNetCore.Mvc;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;

namespace SatelliteTracker.API.Controllers;

//This controller manages notes associated with passes. It allows clients to create, read, update, and delete notes for specific passes.
//Each note is linked to a pass via the PassId property. The controller uses an INoteRepository to interact with the database and perform CRUD operations on notes.
//Error handling is implemented to return appropriate HTTP status codes based on the success or failure of each operation.
[ApiController]
public class NotesController : BaseController
{
    private readonly INoteRepository _repo; // Repository for managing notes in the database

    public NotesController(INoteRepository repo) => _repo = repo; // Constructor that injects the note repository


    // GET /api/passes/{passId}/notes - Retrieves all notes associated with a specific pass
    [HttpGet("/api/passes/{passId:guid}/notes")]
    public async Task<IActionResult> GetByPassId(Guid passId)
    {
        var result = await _repo.GetByPassIdAsync(passId);
        if (!result.IsSuccess) return ToError(result.Error!);
        return Ok(result.Value!.Select(NoteDto.From));
    }


    // POST /api/passes/{passId}/notes - Creates a new note for a specific pass
    [HttpPost("/api/passes/{passId:guid}/notes")]
    public async Task<IActionResult> Add(Guid passId, [FromBody] CreateNoteRequest request)
    {
        var note = new Note
        {
            Id = Guid.NewGuid(),
            PassId = passId,
            Content = request.Content,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _repo.AddAsync(note);
        if (!result.IsSuccess) return ToError(result.Error!);
        return Ok(NoteDto.From(result.Value!));
    }

    // PUT /api/notes/{id} - Updates an existing note by its ID
    [HttpPut("/api/notes/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateNoteRequest request)
    {
        var getResult = await _repo.GetByIdAsync(id);
        if (!getResult.IsSuccess) return ToError(getResult.Error!);

        var note = getResult.Value!;
        note.Content = request.Content;
        note.UpdatedAt = DateTime.UtcNow;

        var result = await _repo.UpdateAsync(note);
        if (!result.IsSuccess) return ToError(result.Error!);
        return Ok(NoteDto.From(result.Value!));
    }

    // DELETE /api/notes/{id} - Deletes a note by its ID
    [HttpDelete("/api/notes/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _repo.DeleteAsync(id);
        if (!result.IsSuccess) return ToError(result.Error!);
        return Ok();
    }

   
    
}
