using Microsoft.EntityFrameworkCore;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

public class SatelliteRepository : ISatelliteRepository
{
    private readonly AppDbContext _context;

    public SatelliteRepository(AppDbContext context) => _context = context;

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

    public async Task<Result<Satellite>> GetByIdAsync(Guid id)
    {
        try
        {
            var satellite = await _context.Satellites.FindAsync(id);
            return satellite is null
                ? Result<Satellite>.Failure($"Satellite {id} not found.")
                : Result<Satellite>.Success(satellite);
        }
        catch (Exception ex)
        {
            return Result<Satellite>.Failure(ex.Message);
        }
    }

    public async Task<Result<Satellite>> GetByNoradIdAsync(int noradId)
    {
        try
        {
            var satellite = await _context.Satellites.FirstOrDefaultAsync(s => s.NoradId == noradId);
            return satellite is null
                ? Result<Satellite>.Failure($"Satellite with NORAD ID {noradId} not found.")
                : Result<Satellite>.Success(satellite);
        }
        catch (Exception ex)
        {
            return Result<Satellite>.Failure(ex.Message);
        }
    }

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
