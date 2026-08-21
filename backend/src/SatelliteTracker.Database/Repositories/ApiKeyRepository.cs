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

    public async Task<Result<ApiKey>> GetByHashAsync(string keyHash)
    {
        try
        {
            var apiKey = await _context.ApiKeys.FirstOrDefaultAsync(a => a.KeyHash == keyHash);
            return apiKey is null
                ? Result<ApiKey>.Failure("No API key found for this hash.")
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

    public async Task<Result> UpdateLastUsedAtAsync(Guid apiKeyId, DateTimeOffset timestamp)
    {
        try
        {
            var apiKey = await _context.ApiKeys.FirstOrDefaultAsync(a => a.Id == apiKeyId);
            if (apiKey is null) return Result.Failure($"ApiKey {apiKeyId} not found.");

            apiKey.LastUsedAt = timestamp;
            await _context.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}
