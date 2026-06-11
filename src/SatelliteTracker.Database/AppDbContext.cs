using Microsoft.EntityFrameworkCore;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Satellite> Satellites => Set<Satellite>();
    public DbSet<TleRecord> TleRecords => Set<TleRecord>();
    public DbSet<Pass> Passes => Set<Pass>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Settings> Settings => Set<Settings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Satellite>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(s => s.NoradId).IsUnique();
        });

        modelBuilder.Entity<TleRecord>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Line1).HasMaxLength(70).IsRequired();
            e.Property(t => t.Line2).HasMaxLength(70).IsRequired();
            e.HasOne(t => t.Satellite)
             .WithMany(s => s.TleRecords)
             .HasForeignKey(t => t.SatelliteId);
        });

        modelBuilder.Entity<Pass>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasOne(p => p.Satellite)
             .WithMany(s => s.Passes)
             .HasForeignKey(p => p.SatelliteId);
            e.HasOne(p => p.TleRecord)
             .WithMany(t => t.Passes)
             .HasForeignKey(p => p.TleId);
        });

        modelBuilder.Entity<Note>(e =>
        {
            e.HasKey(n => n.Id);
            e.HasOne(n => n.Pass)
             .WithMany(p => p.Notes)
             .HasForeignKey(n => n.PassId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
