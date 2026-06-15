using Microsoft.AspNetCore.Mvc;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;

namespace SatelliteTracker.API.Controllers;


//This controller provides CRUD operations for managing satellites.
//It uses an ISatelliteRepository to interact with the database and defines endpoints for retrieving all satellites,
//retrieving a satellite by ID, adding a new satellite, and updating an existing satellite.
//The controller also includes a helper method to convert repository results into appropriate HTTP responses,
//mapping entities to DTOs when successful or returning error responses when not.
[ApiController]
[Route("api/satellites")]
public class SatellitesController : BaseController
{
    private readonly ISatelliteRepository _repo; // Repository for satellite data access

    public SatellitesController(ISatelliteRepository repo) => _repo = repo; // Constructor injection of the satellite repository


    // GET: api/satellites
    // returns a list of all satellites.
    // It retrieves the data from the repository and maps it to a list of SatelliteDto objects before returning an OK response.
    // If the repository operation fails, it returns an appropriate error response.
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _repo.GetAllAsync();
        return Respond(result, satellites => satellites.Select(SatelliteDto.From));
    }

    // GET: api/satellites/{id}
    // retrieves a satellite by its unique identifier (GUID).
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _repo.GetByIdAsync(id);
        return Respond(result, SatelliteDto.From);
    }

    // POST: api/satellites 
    // accepts a CreateSatelliteRequest object in the request body, creates a new Satellite entity, and adds it to the database using the repository.
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] CreateSatelliteRequest request)
    {
        var satellite = new Satellite
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            NoradId = request.NoradId,
            Description = request.Description,
            IsActive = true,
            IsDefault = request.IsDefault,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _repo.AddAsync(satellite);
        return Respond(result, SatelliteDto.From);
    }


    // PUT: api/satellites/{id}
    // updates an existing satellite by its unique identifier (GUID) using the data provided in the request body.
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSatelliteRequest request)
    {
        var getResult = await _repo.GetByIdAsync(id);
        if (!getResult.IsSuccess)
            return ToError(getResult.Error!);

        var satellite = getResult.Value!;
        if (request.Name is not null) satellite.Name = request.Name;
        if (request.Description is not null) satellite.Description = request.Description;
        if (request.IsActive.HasValue) satellite.IsActive = request.IsActive.Value;
        if (request.IsDefault.HasValue) satellite.IsDefault = request.IsDefault.Value;

        var result = await _repo.UpdateAsync(satellite);
        return Respond(result, SatelliteDto.From);
    }

    // Helper method to convert a Result<TEntity> into an IActionResult, mapping the entity to a DTO using the provided mapping function if the result is successful, or returning an error response if it is not.
    private IActionResult Respond<TEntity, TDto>(Result<TEntity> result, Func<TEntity, TDto> map)
    {
        if (result.IsSuccess) return Ok(map(result.Value!));
        return ToError(result.Error!);
    }

   
    
}
