using Microsoft.EntityFrameworkCore;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

// This repository manages satellite data, providing methods to retrieve, add, and update satellite records in the database.
public class SatelliteRepository : ISatelliteRepository
{
    private readonly AppDbContext _context; // The database context used to interact with the Satellites table in the database.

    public SatelliteRepository(AppDbContext context) => _context = context; // Constructor that initializes the repository with the provided database context.



    // Retrieves ALL satellite records from the database.
    public async Task<Result<IEnumerable<Satellite>>> GetAllAsync()
    {
        try
        {
            var satellites = await _context.Satellites.ToListAsync();
            return Result<IEnumerable<Satellite>>.Success(satellites);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<Satellite>>.Failure(ex.Message);
        }
    }



    // Retrieves a satellite record by its unique identifier (ID).
    public async Task<Result<Satellite>> GetByIdAsync(Guid id)
    {
        try
        {
            var satellite = await _context.Satellites.FindAsync(id);

            // If the satellite is not found, return a failure result with an appropriate error message.
            return satellite is null? Result<Satellite>.Failure($"Satellite {id} not found."): Result<Satellite>.Success(satellite);
        }
        catch (Exception ex)
        {
            return Result<Satellite>.Failure(ex.Message);
        }
    }



    // Retrieves a satellite record by its NORAD ID, which is a unique identifier assigned to satellites by the North American Aerospace Defense Command.
    public async Task<Result<Satellite>> GetByNoradIdAsync(int noradId)
    {
        try
        {
            var satellite = await _context.Satellites.FirstOrDefaultAsync(s => s.NoradId == noradId);
            // If the satellite is not found, return a failure result with an appropriate error message. else, return a success result containing the satellite data.
            return satellite is null? Result<Satellite>.Failure($"Satellite with NORAD ID {noradId} not found."): Result<Satellite>.Success(satellite);
        }
        catch (Exception ex)
        {
            return Result<Satellite>.Failure(ex.Message);
        }
    }



    
    // Retrieves all active satellite records from the database.
    public async Task<Result<IEnumerable<Satellite>>> GetActiveAsync()
    {
        try
        {
            var satellites = await _context.Satellites.Where(s => s.IsActive).ToListAsync();
            return Result<IEnumerable<Satellite>>.Success(satellites);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<Satellite>>.Failure(ex.Message);
        }
    }




    // Adds a new satellite record to the database.
    public async Task<Result<Satellite>> AddAsync(Satellite satellite)
    {
        try
        {
            _context.Satellites.Add(satellite);
            await _context.SaveChangesAsync();
            return Result<Satellite>.Success(satellite);
        }
        catch (Exception ex)
        {
            return Result<Satellite>.Failure(ex.Message);
        }
    }



    // Updates an existing satellite record in the database.
    public async Task<Result<Satellite>> UpdateAsync(Satellite satellite)
    {
        try
        {
            _context.Satellites.Update(satellite);
            await _context.SaveChangesAsync();
            return Result<Satellite>.Success(satellite);
        }
        catch (Exception ex)
        {
            return Result<Satellite>.Failure(ex.Message);
        }
    }
}
