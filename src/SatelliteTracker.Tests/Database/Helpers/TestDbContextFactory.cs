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

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new TestAppDbContext(options);
        context.Database.EnsureCreated();
        return (context, connection);
    }
}
