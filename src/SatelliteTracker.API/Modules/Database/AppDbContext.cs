using Microsoft.EntityFrameworkCore;
using SatelliteTracker.API.Modules.Database.Entities;

namespace SatelliteTracker.API.Modules.Database;
//The AppDbContext class is responsible for managing the database context and providing access to the entities defined in the application.
//It inherits from the DbContext class provided by Entity Framework Core, which allows it to interact with the database using LINQ queries and other EF Core features.
//The OnModelCreating method is overridden to configure the entity relationships and constraints using the Fluent API, ensuring that the database schema is properly defined based on the entity classes.
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    //properties representing the tables in the database, allowing for CRUD operations on the corresponding entities
    public DbSet<Satellite> Satellites => Set<Satellite>();  // DbSet for the Satellite entity, representing the Satellites table in the database
    public DbSet<TleRecord> TleRecords => Set<TleRecord>(); // DbSet for the TleRecord entity, representing the TleRecords table in the database
    public DbSet<Pass> Passes => Set<Pass>(); // DbSet for the Pass entity, representing the Passes table in the database
    public DbSet<Note> Notes => Set<Note>(); // DbSet for the Note entity, representing the Notes table in the database
    public DbSet<Settings> Settings => Set<Settings>(); // DbSet for the Settings entity, representing the Settings table in the database

    
    // The OnModelCreating method is used to configure the entity relationships and constraints using the Fluent API
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

