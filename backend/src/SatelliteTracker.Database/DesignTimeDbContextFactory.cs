using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SatelliteTracker.Database;

// Lets the EF Core tools (dotnet ef migrations add/list/bundle) create AppDbContext without
// running the API's Program.cs. Without it, the tools build the API host, which needs the
// validated configuration (I2.2), and the Docker build stage has no secrets at all.
//
// EF prefers this factory over the API host whenever it exists, so every design-time command
// gets this placeholder connection string. That is fine for commands that only need the model
// (migrations add/remove, bundle creation): they never connect. Commands that do connect take
// the real connection string at run time instead:
//   dotnet ef database update --connection "<connection-string>" ...
//   ./efbundle --connection "<connection-string>"
// "dotnet ef migrations list" tries to connect to show applied status; pass --no-connect to skip
// that, or --connection to check a real database.
//
// UseAppNpgsql keeps the provider and retry policy identical to the API, so the model the tools
// see (and a bundle's behavior against a real database) matches production.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    // Not a secret and not a real database: no password, and the .invalid TLD (RFC 6761) never
    // resolves, so a design-time command can never reach a real server by accident.
    internal const string PlaceholderConnectionString =
        "Host=design-time.invalid;Database=design_time_placeholder;Username=design_time_placeholder";

    public AppDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        builder.UseAppNpgsql(PlaceholderConnectionString);

        return new AppDbContext(builder.Options);
    }
}
