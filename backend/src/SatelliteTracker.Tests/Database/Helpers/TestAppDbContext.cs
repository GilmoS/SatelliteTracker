using Microsoft.EntityFrameworkCore;
using SatelliteTracker.Database;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Tests.Database.Helpers;

internal class TestAppDbContext : AppDbContext
{
    public TestAppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SQLite does not support int[] — serialize as comma-separated string
        modelBuilder.Entity<Settings>()
            .Property(s => s.AlertMinutes)
            .HasConversion(
                v => string.Join(',', v),
                v => string.IsNullOrEmpty(v)
                    ? Array.Empty<int>()
                    : v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(int.Parse).ToArray()
            );
    }
}
