using Microsoft.EntityFrameworkCore;
using SatelliteTracker.Database.Entities;

namespace SatelliteTracker.Database;


//This class represents the database context for the Satellite Tracker application.
//It inherits from DbContext and provides access to the various entities in the database, such as Satellites, TleRecords, Passes, Notes, and Settings.
//The OnModelCreating method is overridden to configure the entity relationships and constraints using Fluent API.
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { } // Constructor that accepts DbContextOptions and passes them to the base class constructor

    // DbSet properties for each entity in the database
    public DbSet<Satellite> Satellites => Set<Satellite>();
    public DbSet<TleRecord> TleRecords => Set<TleRecord>();
    public DbSet<Pass> Passes => Set<Pass>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Settings> Settings => Set<Settings>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<PassSubscription> PassSubscriptions => Set<PassSubscription>();
    public DbSet<PassNotificationLog> PassNotificationLogs => Set<PassNotificationLog>();
    public DbSet<AllowlistedEmail> AllowlistedEmails => Set<AllowlistedEmail>();

    // The OnModelCreating method is overridden to configure the entity relationships and constraints using Fluent API.
    // It defines the primary keys, property constraints, and relationships between the entities.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
       ConfigureEntities(modelBuilder);
       SeedData(modelBuilder);
    }

    protected virtual void SeedData(ModelBuilder modelBuilder)  //virtual!
{
    modelBuilder.Entity<Satellite>().HasData(
        new Satellite
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "EROS C3",
            NoradId = 54880,
            Description = "EROS C3 observation satellite",
            IsActive = true,
            IsDefault = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        },
        new Satellite
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = "RUNNER-1",
            NoradId = 56953,
            Description = "RUNNER-1 observation satellite",
            IsActive = true,
            IsDefault = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        }
    );
}

private static void ConfigureEntities(ModelBuilder modelBuilder)
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
    modelBuilder.Entity<ApiKey>(e =>
    {
        e.HasKey(a => a.Id);
        e.Property(a => a.Email).HasMaxLength(256).IsRequired();
        e.Property(a => a.DisplayName).HasMaxLength(100).IsRequired();
        e.Property(a => a.KeyHash).HasMaxLength(64).IsRequired();
    });
    modelBuilder.Entity<UserSettings>(e =>
    {
        e.HasKey(u => u.Id);
        e.HasOne(u => u.ApiKey)
         .WithOne(a => a.UserSettings)
         .HasForeignKey<UserSettings>(u => u.ApiKeyId);
        // Enforces the 1:1 with ApiKey — the navigation property alone does not.
        e.HasIndex(u => u.ApiKeyId).IsUnique();
    });
    modelBuilder.Entity<PassSubscription>(e =>
    {
        e.HasKey(s => s.Id);
        e.HasOne(s => s.Pass)
         .WithMany(p => p.PassSubscriptions)
         .HasForeignKey(s => s.PassId)
         .OnDelete(DeleteBehavior.Cascade);
        e.HasOne(s => s.ApiKey)
         .WithMany(a => a.PassSubscriptions)
         .HasForeignKey(s => s.ApiKeyId)
         .OnDelete(DeleteBehavior.Cascade);
        // Sparse exception table: at most one override row per (pass, tester).
        e.HasIndex(s => new { s.PassId, s.ApiKeyId }).IsUnique();
    });
    modelBuilder.Entity<PassNotificationLog>(e =>
    {
        e.HasKey(l => l.Id);
        e.HasOne(l => l.Pass)
         .WithMany(p => p.PassNotificationLogs)
         .HasForeignKey(l => l.PassId)
         .OnDelete(DeleteBehavior.Cascade);
        e.HasOne(l => l.ApiKey)
         .WithMany(a => a.PassNotificationLogs)
         .HasForeignKey(l => l.ApiKeyId)
         .OnDelete(DeleteBehavior.Cascade);
        // Append-only ledger: one row per (pass, tester, threshold) that actually fired.
        e.HasIndex(l => new { l.PassId, l.ApiKeyId, l.AlertMinutes }).IsUnique();
    });
    modelBuilder.Entity<AllowlistedEmail>(e =>
    {
        e.HasKey(a => a.Id);
        e.Property(a => a.Email).HasMaxLength(256).IsRequired();
        // Uniqueness relies on every write normalizing (trim+lowercase) first — see entity comment.
        e.HasIndex(a => a.Email).IsUnique();
    });
}

}
