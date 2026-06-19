using Microsoft.EntityFrameworkCore;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

//This repository manages TLE (Two-Line Element) records,
//providing methods to retrieve the latest TLE for a satellite,
//retrieve historical TLEs, add new TLE records, and delete old TLE records from the database.
public class TleRepository : ITleRepository
{
    private readonly AppDbContext _context; // The database context used to interact with the TleRecords table in the database.

    public TleRepository(AppDbContext context) => _context = context; // Constructor that initializes the repository with the provided database context.



    // Retrieves the latest TLE record for a satellite identified by its NORAD ID.
    public async Task<Result<TleRecord>> GetLatestByNoradIdAsync(int noradId)
    {
        try
        {
            var record = await _context.TleRecords
                .Include(t => t.Satellite)
                .Where(t => t.Satellite.NoradId == noradId)
                .OrderByDescending(t => t.FetchedAt)
                .FirstOrDefaultAsync();

            // If no record is found, return a failure result with an appropriate message. else, return a success result containing the latest TLE record.
            return record is null? Result<TleRecord>.Failure($"No TLE record found for NORAD ID {noradId}."): Result<TleRecord>.Success(record);
        }
        catch (Exception ex)
        {
            return Result<TleRecord>.Failure(ex.Message);
        }
    }

    // Retrieves historical TLE records for a satellite identified by its NORAD ID,
    public async Task<Result<IEnumerable<TleRecord>>> GetHistoryAsync(int noradId, DateTime from)
    {
        try
        {
            var records = await _context.TleRecords
                .Include(t => t.Satellite)
                .Where(t => t.Satellite.NoradId == noradId && t.FetchedAt >= from)
                .OrderByDescending(t => t.FetchedAt)
                .ToListAsync();

            return Result<IEnumerable<TleRecord>>.Success(records);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<TleRecord>>.Failure(ex.Message);
        }
    }


    // Adds a new TLE record to the database.
    public async Task<Result<TleRecord>> AddAsync(TleRecord tle)
    {
        try
        {
            _context.TleRecords.Add(tle);
            await _context.SaveChangesAsync();
            return Result<TleRecord>.Success(tle);
        }
        catch (Exception ex)
        {
            return Result<TleRecord>.Failure(ex.Message);
        }
    }


    // Deletes TLE records older than a specified cutoff date that do not have any associated passes.
    //set to six months as requested by the clint, but can be changed to any other value as needed.
    public async Task<Result<bool>> DeleteOlderThanAsync(DateTime cutoff)
    {
        try
        {
            var old = await _context.TleRecords
                .Where(t => t.FetchedAt < cutoff && !t.Passes.Any())
                .ToListAsync();

            _context.TleRecords.RemoveRange(old);
            await _context.SaveChangesAsync();
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(ex.Message);
        }
    }
}
