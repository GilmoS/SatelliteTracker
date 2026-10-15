using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SatelliteTracker.Database;

namespace SatelliteTracker.Tests.Database.Helpers;

internal static class TestDbContextFactory
{
    public static (AppDbContext context, SqliteConnection connection) Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var context = new TestAppDbContext(BuildOptions(connection));
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
        return (context, connection);
    }

    // A second, untracked context against the same in-memory connection — needed to exercise
    // DB-level constraints (e.g. unique indexes) that EF's own change tracker would otherwise
    // paper over via relationship fixup within a single context.
    public static AppDbContext CreateAdditionalContext(SqliteConnection connection) =>
        new TestAppDbContext(BuildOptions(connection));

    private static DbContextOptions<AppDbContext> BuildOptions(SqliteConnection connection) =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
}
