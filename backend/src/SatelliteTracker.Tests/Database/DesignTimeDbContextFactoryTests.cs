using Microsoft.EntityFrameworkCore;
using Npgsql;
using SatelliteTracker.Database;
using Xunit;

namespace SatelliteTracker.Tests.Database;

// The factory the EF tools (and the Docker migration bundle) use instead of Program.cs.
// None of these tests opens a connection.
public class DesignTimeDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_UsesNpgsqlWithTheProductionRetryPolicy()
    {
        using var context = new DesignTimeDbContextFactory().CreateDbContext([]);

        Assert.True(context.Database.IsNpgsql());
        Assert.True(context.Database.CreateExecutionStrategy().RetriesOnFailure);
    }

    [Fact]
    public void CreateDbContext_PlaceholderHasNoPasswordAndAnUnresolvableHost()
    {
        using var context = new DesignTimeDbContextFactory().CreateDbContext([]);

        var builder = new NpgsqlConnectionStringBuilder(context.Database.GetConnectionString());

        Assert.Null(builder.Password);
        Assert.EndsWith(".invalid", builder.Host);
    }

    // The migrations (and so the bundle built from them) must match the model. Fails when an
    // entity or its configuration changed without a new migration.
    [Fact]
    public void Model_HasNoChangesMissingFromTheMigrations()
    {
        using var context = new DesignTimeDbContextFactory().CreateDbContext([]);

        Assert.False(context.Database.HasPendingModelChanges());
    }
}
