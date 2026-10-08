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

    // Retrieves historical satellite passes for a specific satellite that occurred after a
    // specified date, applying query's optional per-field filters (AND-combined) server-side via
    // EF Core query composition, ordered by Aos descending, and paged. HasMore is determined by
    // fetching PageSize+1 rows rather than a separate COUNT query.
    public async Task<Result<PagedResult<Pass>>> GetHistoryAsync(Guid satelliteId, DateTime from, PassHistoryQuery query)
    {
        try
        {
            var passesQuery = _context.Passes
                .Where(p => p.SatelliteId == satelliteId && p.Los < DateTime.UtcNow && p.Aos >= from);

            if (query.OrbitNumberFrom.HasValue)
                passesQuery = passesQuery.Where(p => p.OrbitNumber >= query.OrbitNumberFrom.Value);
            if (query.OrbitNumberTo.HasValue)
                passesQuery = passesQuery.Where(p => p.OrbitNumber <= query.OrbitNumberTo.Value);
            if (query.MaxElevationFrom.HasValue)
                passesQuery = passesQuery.Where(p => p.MaxElevation >= query.MaxElevationFrom.Value);
            if (query.MaxElevationTo.HasValue)
                passesQuery = passesQuery.Where(p => p.MaxElevation <= query.MaxElevationTo.Value);
            if (query.AosFrom.HasValue)
                passesQuery = passesQuery.Where(p => p.Aos >= query.AosFrom.Value);
            if (query.AosTo.HasValue)
                passesQuery = passesQuery.Where(p => p.Aos <= query.AosTo.Value);
            if (query.LosFrom.HasValue)
                passesQuery = passesQuery.Where(p => p.Los >= query.LosFrom.Value);
            if (query.LosTo.HasValue)
                passesQuery = passesQuery.Where(p => p.Los <= query.LosTo.Value);

            var passes = await passesQuery
                .OrderByDescending(p => p.Aos)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize + 1)
                .ToListAsync();

            var hasMore = passes.Count > query.PageSize;
            var items = hasMore ? passes.Take(query.PageSize).ToList() : passes;

            return Result<PagedResult<Pass>>.Success(new PagedResult<Pass>(items, query.Page, query.PageSize, hasMore));
        }
        catch (Exception ex)
        {
            return Result<PagedResult<Pass>>.Failure(ex.Message);
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

    // Returns all future passes; per-tester opt-out/already-sent filtering now happens in
    // PassNotificationJob against PassSubscription and PassNotificationLog.
    public async Task<Result<IEnumerable<Pass>>> GetPendingNotificationsAsync()
    {
        try
        {
            var passes = await _context.Passes
                .Include(p => p.Satellite)
                .Where(p => p.Aos > DateTime.UtcNow)
                .OrderBy(p => p.Aos)
                .ToListAsync();

            return Result<IEnumerable<Pass>>.Success(passes);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<Pass>>.Failure(ex.Message);
        }
    }

    // Retrieves passes by ID with Satellite eager-loaded; fails clearly if any ID is missing.
    public async Task<Result<IReadOnlyList<Pass>>> GetByIdsAsync(IEnumerable<Guid> passIds)
    {
        try
        {
            var ids = passIds.ToList();

            var passes = await _context.Passes
                .Include(p => p.Satellite)
                .Where(p => ids.Contains(p.Id))
                .ToListAsync();

            var foundIds = passes.Select(p => p.Id).ToHashSet();
            var missingIds = ids.Where(id => !foundIds.Contains(id)).ToList();
            if (missingIds.Count > 0)
            {
                return Result<IReadOnlyList<Pass>>.Failure(
                    $"Pass(es) not found: {string.Join(", ", missingIds)}.");
            }

            return Result<IReadOnlyList<Pass>>.Success(passes);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Pass>>.Failure(ex.Message);
        }
    }

    // Bulk-marks the given passes as synced. See IPassRepository.MarkOutlookSyncedAsync for the
    // ICS-MVP semantics of the OutlookSynced flag.
    public async Task<Result> MarkOutlookSyncedAsync(IEnumerable<Guid> passIds)
    {
        try
        {
            var ids = passIds.ToList();

            var passes = await _context.Passes
                .Where(p => ids.Contains(p.Id))
                .ToListAsync();

            foreach (var pass in passes)
                pass.OutlookSynced = true;

            await _context.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    // Upserts a satellite's upcoming passes by (SatelliteId, OrbitNumber) in one transaction —
    // see IPassRepository.UpsertUpcomingAsync for the full semantics.
    public async Task<Result<PassUpsertResult>> UpsertUpcomingAsync(Guid satelliteId, DateTime now, IReadOnlyList<Pass> computed)
    {
        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var upcoming = await _context.Passes
                .Where(p => p.SatelliteId == satelliteId && p.Aos >= now)
                .ToDictionaryAsync(p => p.OrbitNumber);

            // Orbits already owned by a past/in-progress row: out of this job's scope.
            var computedOrbits = computed.Select(p => p.OrbitNumber).ToList();
            var pastOrbits = (await _context.Passes
                .Where(p => p.SatelliteId == satelliteId && p.Aos < now && computedOrbits.Contains(p.OrbitNumber))
                .Select(p => p.OrbitNumber)
                .ToListAsync()).ToHashSet();

            int inserted = 0, updated = 0, skippedPast = 0;
            var kept = new HashSet<int>();

            foreach (var pass in computed)
            {
                if (pastOrbits.Contains(pass.OrbitNumber))
                {
                    skippedPast++;
                    continue;
                }

                if (upcoming.TryGetValue(pass.OrbitNumber, out var existing))
                {
                    existing.Aos = pass.Aos;
                    existing.Los = pass.Los;
                    existing.MaxElevation = pass.MaxElevation;
                    existing.AosAzimuth = pass.AosAzimuth;
                    existing.LosAzimuth = pass.LosAzimuth;
                    existing.DurationSec = pass.DurationSec;
                    existing.TleId = pass.TleId;
                    existing.CalculatedAt = pass.CalculatedAt;
                    kept.Add(pass.OrbitNumber);
                    updated++;
                }
                else
                {
                    _context.Passes.Add(pass);
                    inserted++;
                }
            }

            // Upcoming passes the new computation no longer predicts won't happen; deleting them
            // cascades their notes, subscriptions and notification logs, which is intended.
            var vanished = upcoming.Values.Where(p => !kept.Contains(p.OrbitNumber)).ToList();
            _context.Passes.RemoveRange(vanished);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Result<PassUpsertResult>.Success(new PassUpsertResult(inserted, updated, vanished.Count, skippedPast));
        }
        catch (Exception ex)
        {
            // The transaction rolled back; drop the half-applied tracked changes too, so a later
            // SaveChanges on this context can't commit them.
            _context.ChangeTracker.Clear();
            return Result<PassUpsertResult>.Failure(ex.Message);
        }
    }
}
