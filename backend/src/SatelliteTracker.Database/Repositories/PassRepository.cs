using Microsoft.EntityFrameworkCore;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

//This repository manages satellite pass data, providing methods to retrieve upcoming and historical passes, as well as adding and updating pass records.
public class PassRepository : IPassRepository
{
    private readonly AppDbContext _context; // The database context used to interact with the Passes table in the database.

    public PassRepository(AppDbContext context) => _context = context; // Constructor that initializes the repository with the provided database context.

    // Retrieves upcoming satellite passes for a specific satellite within a given time range.
    public async Task<Result<IEnumerable<Pass>>> GetUpcomingAsync(Guid satelliteId, DateTime from, DateTime to)
    {
        try
        {
            var passes = await _context.Passes
                .Where(p => p.SatelliteId == satelliteId && p.Aos >= from && p.Aos <= to)
                .OrderBy(p => p.Aos)
                .ToListAsync();

            return Result<IEnumerable<Pass>>.Success(passes);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<Pass>>.Failure(ex.Message);
        }
    }

    // Retrieves historical satellite passes for a specific satellite that occurred after a specified date.
    public async Task<Result<IEnumerable<Pass>>> GetHistoryAsync(Guid satelliteId, DateTime from)
    {
        try
        {
            var passes = await _context.Passes
                .Where(p => p.SatelliteId == satelliteId && p.Los < DateTime.UtcNow && p.Aos >= from)
                .OrderByDescending(p => p.Aos)
                .ToListAsync();

            return Result<IEnumerable<Pass>>.Success(passes);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<Pass>>.Failure(ex.Message);
        }
    }

    // Retrieves a specific satellite pass by its unique identifier.
    public async Task<Result<Pass>> GetByIdAsync(Guid id)
    {
        try
        {
            var pass = await _context.Passes.FindAsync(id);
            // If the pass is not found, return a failure result with an appropriate message; otherwise, return a success result containing the pass.
            return pass is null ? Result<Pass>.Failure($"Pass {id} not found.") : Result<Pass>.Success(pass);
        }
        catch (Exception ex)
        {
            return Result<Pass>.Failure(ex.Message);
        }
    }

    // Adds a new satellite pass record to the database and returns the added pass if successful.
    public async Task<Result<Pass>> AddAsync(Pass pass)
    {
        try
        {
            _context.Passes.Add(pass);
            await _context.SaveChangesAsync();
            return Result<Pass>.Success(pass);
        }
        catch (Exception ex)
        {
            return Result<Pass>.Failure(ex.Message);
        }
    }

    // Adds multiple satellite pass records to the database and returns a success result if all passes are added successfully.
    public async Task<Result<bool>> AddRangeAsync(IEnumerable<Pass> passes)
    {
        try
        {
            _context.Passes.AddRange(passes);
            await _context.SaveChangesAsync();
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(ex.Message);
        }
    }

    // Updates an existing satellite pass record in the database and returns the updated pass if successful.
    public async Task<Result<Pass>> UpdateAsync(Pass pass)
    {
        try
        {
            _context.Passes.Update(pass);
            await _context.SaveChangesAsync();
            return Result<Pass>.Success(pass);
        }
        catch (Exception ex)
        {
            return Result<Pass>.Failure(ex.Message);
        }
    }

    // Updates the Notify flag for a specific pass by its unique identifier.
    public async Task<Result<Pass>> UpdateNotifyAsync(Guid id, bool notify)
    {
        try
        {
            var pass = await _context.Passes.FindAsync(id);
            if (pass is null)
                return Result<Pass>.Failure("Pass not found");

            pass.Notify = notify;
            await _context.SaveChangesAsync();
            return Result<Pass>.Success(pass);
        }
        catch (Exception ex)
        {
            return Result<Pass>.Failure(ex.Message);
        }
    }

    // Deletes upcoming predicted passes for a satellite (passes with AOS >= from)
    public async Task<Result<bool>> DeleteUpcomingAsync(Guid satelliteId, DateTime from)
    {
        try
        {
            var upcoming = await _context.Passes
                .Where(p => p.SatelliteId == satelliteId && p.Aos >= from)
                .ToListAsync();

            if (!upcoming.Any())
                return Result<bool>.Success(true);

            _context.Passes.RemoveRange(upcoming);
            await _context.SaveChangesAsync();
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(ex.Message);
        }
    }
}
