using Microsoft.EntityFrameworkCore;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

public class AllowlistedEmailRepository : IAllowlistedEmailRepository
{
    private readonly AppDbContext _context;

    public AllowlistedEmailRepository(AppDbContext context) => _context = context;

    public async Task<Result<bool>> ExistsAsync(string normalizedEmail)
    {
        try
        {
            var exists = await _context.AllowlistedEmails.AnyAsync(a => a.Email == normalizedEmail);
            return Result<bool>.Success(exists);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(ex.Message);
        }
    }

    // Duplicate add is treated as a no-op success (admin convenience tool, not a strict API
    // contract) — return the existing row instead of inserting a second one or failing.
    public async Task<Result<AllowlistedEmail>> AddAsync(string normalizedEmail)
    {
        try
        {
            var existing = await _context.AllowlistedEmails.FirstOrDefaultAsync(a => a.Email == normalizedEmail);
            if (existing is not null)
                return Result<AllowlistedEmail>.Success(existing);

            var entry = new AllowlistedEmail
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                AddedAt = DateTimeOffset.UtcNow
            };

            _context.AllowlistedEmails.Add(entry);
            await _context.SaveChangesAsync();
            return Result<AllowlistedEmail>.Success(entry);
        }
        catch (Exception ex)
        {
            return Result<AllowlistedEmail>.Failure(ex.Message);
        }
    }

    public async Task<Result<IEnumerable<AllowlistedEmail>>> GetAllAsync()
    {
        try
        {
            // Ordered client-side: SQLite (used in tests) can't translate ORDER BY over
            // DateTimeOffset, and this table is small enough that it doesn't matter.
            var all = (await _context.AllowlistedEmails.ToListAsync())
                .OrderBy(a => a.AddedAt)
                .ToList();
            return Result<IEnumerable<AllowlistedEmail>>.Success(all);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<AllowlistedEmail>>.Failure(ex.Message);
        }
    }
}
