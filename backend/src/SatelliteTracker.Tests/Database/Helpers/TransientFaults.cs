using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using SatelliteTracker.Database;

namespace SatelliteTracker.Tests.Database.Helpers;

// Stand-ins for production's Npgsql retrying strategy (DatabaseRetryPolicy) on SQLite: a retrying
// execution strategy that treats SimulatedTransientException as transient, and interceptors that
// inject one such fault at a chosen point. They show what a replay does to a unit of work.
internal sealed class SimulatedTransientException() : Exception("Simulated transient database failure.");

internal sealed class TestRetryingExecutionStrategy(ExecutionStrategyDependencies dependencies)
    : ExecutionStrategy(dependencies, maxRetryCount: 3, maxRetryDelay: TimeSpan.Zero)
{
    protected override bool ShouldRetryOn(Exception exception) => exception is SimulatedTransientException;
}

// Throws once, right after the first command whose SQL matches has run. For a single statement
// in autocommit that is a lost acknowledgement: the statement took effect, the caller never heard
// back.
internal sealed class FailOnceAfterCommandInterceptor(Func<string, bool> matches) : DbCommandInterceptor
{
    public bool Fired { get; private set; }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (ShouldFire(command))
        {
            await result.DisposeAsync();
            throw new SimulatedTransientException();
        }
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default) =>
        ShouldFire(command) ? throw new SimulatedTransientException() : ValueTask.FromResult(result);

    private bool ShouldFire(DbCommand command)
    {
        if (Fired || !matches(command.CommandText)) return false;
        Fired = true;
        return true;
    }
}

// Throws once at commit time. Before the commit: the transaction rolls back after SaveChanges
// already accepted its changes into the context. After the commit: the work is in the database
// but the commit's acknowledgement was lost.
internal sealed class FailOnceAtCommitInterceptor(bool afterCommit) : DbTransactionInterceptor
{
    public bool Fired { get; private set; }

    public override ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (!afterCommit) FireOnce();
        return ValueTask.FromResult(result);
    }

    public override Task TransactionCommittedAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (afterCommit) FireOnce();
        return Task.CompletedTask;
    }

    private void FireOnce()
    {
        if (Fired) return;
        Fired = true;
        throw new SimulatedTransientException();
    }
}

internal static class RetryingTestDbContext
{
    public static AppDbContext Create(SqliteConnection connection, params IInterceptor[] interceptors) =>
        new TestAppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection, sqlite => sqlite.ExecutionStrategy(d => new TestRetryingExecutionStrategy(d)))
            .AddInterceptors(interceptors)
            .Options);
}
