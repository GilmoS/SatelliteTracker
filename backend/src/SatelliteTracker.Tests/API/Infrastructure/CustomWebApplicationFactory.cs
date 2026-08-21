using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SatelliteTracker.Database;
using SatelliteTracker.PassService.Services;
using SatelliteTracker.TLEService.Jobs;
using SatelliteTracker.API.Jobs;
using SatelliteTracker.Tests.Database.Helpers;

namespace SatelliteTracker.Tests.API.Infrastructure;

// Boots the real API host (full middleware pipeline, including the ApiKey authentication
// scheme) in-memory for the HTTP-pipeline-level auth tests. Not a replacement for the
// SQLite-in-memory-per-repository unit tests elsewhere — this is specifically for verifying the
// scheme actually works when wired into Program.cs, not in isolation (see AuthenticationTests).
//
// Swaps the production AppDbContext registration for the same SQLite-in-memory + TestAppDbContext
// pattern TestDbContextFactory uses (TestAppDbContext supplies the int[]-as-CSV conversion SQLite
// needs for UserSettings.AlertMinutes, and skips the two-satellite seed data for test isolation).
//
// Registered manually rather than via AddDbContext<AppDbContext, TestAppDbContext> — that generic
// overload wires TContextImplementation's constructor to DbContextOptions<TContextImplementation>,
// but TestAppDbContext's constructor (matching TestDbContextFactory's usage) takes
// DbContextOptions<AppDbContext>, so the generic overload can't resolve it.
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public CustomWebApplicationFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            RemoveService<DbContextOptions<AppDbContext>>(services);
            RemoveService<AppDbContext>(services);

            services.AddScoped(_ => new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options);
            services.AddScoped<AppDbContext>(sp =>
                new TestAppDbContext(sp.GetRequiredService<DbContextOptions<AppDbContext>>()));

            // The three background jobs poll on real-world timers against N2YO/Firebase, which
            // are unavailable/undesirable in tests — they'd otherwise start as soon as the host
            // does (WebApplicationFactory.CreateClient() starts the real host, hosted services
            // included) and race the SQLite in-memory connection tests use.
            RemoveHostedService<TleUpdateJob>(services);
            RemoveHostedService<PassCalculationJob>(services);
            RemoveHostedService<PassNotificationJob>(services);

            using var scope = services.BuildServiceProvider().CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Database.EnsureCreated();
        });
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(T));
        if (descriptor is not null) services.Remove(descriptor);
    }

    private static void RemoveHostedService<T>(IServiceCollection services) where T : class, IHostedService
    {
        var descriptor = services.SingleOrDefault(
            d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(T));
        if (descriptor is not null) services.Remove(descriptor);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
