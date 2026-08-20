using Microsoft.EntityFrameworkCore;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database.Repositories;

public class ApiKeyRepository : IApiKeyRepository
{
    private readonly AppDbContext _context;

    public ApiKeyRepository(AppDbContext context) => _context = context;

    public async Task<Result<ApiKey>> GetActiveByEmailAsync(string normalizedEmail)
    {
        try
        {
            var apiKey = await _context.ApiKeys.FirstOrDefaultAsync(a => a.Email == normalizedEmail && a.IsActive);
            return apiKey is null
                ? Result<ApiKey>.Failure("No active API key found for this email.")
                : Result<ApiKey>.Success(apiKey);
        }
        catch (Exception ex)
        {
            return Result<ApiKey>.Failure(ex.Message);
        }
    }

    public async Task<Result<ApiKey>> CreateAsync(ApiKey apiKey)
    {
        try
        {
            _context.ApiKeys.Add(apiKey);
            await _context.SaveChangesAsync();
            return Result<ApiKey>.Success(apiKey);
        }
        catch (Exception ex)
        {
            return Result<ApiKey>.Failure(ex.Message);
        }
    }
}
