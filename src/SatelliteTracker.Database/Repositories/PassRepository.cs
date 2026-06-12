using Microsoft.EntityFrameworkCore;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

public class PassRepository : IPassRepository
{
    private readonly AppDbContext _context;

    public PassRepository(AppDbContext context) => _context = context;

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

    public async Task<Result<Pass>> GetByIdAsync(Guid id)
    {
        try
        {
            var pass = await _context.Passes.FindAsync(id);
            return pass is null
                ? Result<Pass>.Failure($"Pass {id} not found.")
                : Result<Pass>.Success(pass);
        }
        catch (Exception ex)
        {
            return Result<Pass>.Failure(ex.Message);
        }
    }

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
}
