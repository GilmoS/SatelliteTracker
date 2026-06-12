using Microsoft.EntityFrameworkCore;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

public class TleRepository : ITleRepository
{
    private readonly AppDbContext _context;

    public TleRepository(AppDbContext context) => _context = context;

    public async Task<Result<TleRecord>> GetLatestByNoradIdAsync(int noradId)
    {
        try
        {
            var record = await _context.TleRecords
                .Include(t => t.Satellite)
                .Where(t => t.Satellite.NoradId == noradId)
                .OrderByDescending(t => t.FetchedAt)
                .FirstOrDefaultAsync();

            return record is null
                ? Result<TleRecord>.Failure($"No TLE record found for NORAD ID {noradId}.")
                : Result<TleRecord>.Success(record);
        }
        catch (Exception ex)
        {
            return Result<TleRecord>.Failure(ex.Message);
        }
    }

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
