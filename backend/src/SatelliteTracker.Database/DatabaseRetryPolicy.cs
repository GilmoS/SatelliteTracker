using Microsoft.EntityFrameworkCore;

namespace SatelliteTracker.Database;

// The one place the database retry behavior is defined. Retries are behavior, not configuration:
// they are the same in every environment, so they are code constants. Everything about the
// connection itself (SSL mode, timeouts, keepalive, pool size) lives in the connection string.
//
// With a retrying execution strategy, a transient failure (Azure maintenance, a restart, a
// network blip) replays the operation instead of failing it. A user-initiated transaction must
// then run inside Database.CreateExecutionStrategy().ExecuteAsync(...), so the whole unit is
// replayed; EF throws at runtime otherwise. See PassRepository.UpsertUpcomingAsync.
public static class DatabaseRetryPolicy
{
    public const int MaxRetryCount = 6;
    public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(6);

    public static DbContextOptionsBuilder UseAppNpgsql(this DbContextOptionsBuilder options, string? connectionString) =>
        options.UseNpgsql(connectionString, npgsql =>
            npgsql.EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay, errorCodesToAdd: null));
}
