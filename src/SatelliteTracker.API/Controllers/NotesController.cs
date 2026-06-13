using Microsoft.AspNetCore.Mvc;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;

namespace SatelliteTracker.API.Controllers;

[ApiController]
public class NotesController : ControllerBase
{
    private readonly INoteRepository _repo;

    public NotesController(INoteRepository repo) => _repo = repo;

    [HttpGet("/api/passes/{passId:guid}/notes")]
    public async Task<IActionResult> GetByPassId(Guid passId)
    {
        var result = await _repo.GetByPassIdAsync(passId);
        if (!result.IsSuccess) return ToError(result.Error!);
        return Ok(result.Value!.Select(NoteDto.From));
    }

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

    [HttpDelete("/api/notes/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _repo.DeleteAsync(id);
        if (!result.IsSuccess) return ToError(result.Error!);
        return Ok();
    }

    private IActionResult ToError(string error) =>
        error.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? NotFound(new { error })
            : StatusCode(500, new { error });
}
