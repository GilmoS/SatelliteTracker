using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SatelliteTracker.API.Configuration;
using SatelliteTracker.Tests.API.Infrastructure;
using Xunit;

namespace SatelliteTracker.Tests.API;

// A configuration validation failure at startup must end in exit code 1 with one critical log
// message naming every failed setting (never its value). Anything else must behave as before.
public class HostRunnerTests
{
    [Fact]
    public async Task InvalidConfiguration_LogsOneCriticalMessage_AndReturnsExitCode1()
    {
        const string adminSentinel = "short-admin-sentinel"; // under 32 chars: invalid
        const string latSentinel = "123.456789";              // out of range: invalid
        var logs = new CapturingLoggerProvider();
        using var host = BuildHost(logs, new()
        {
            ["Admin:ApiKey"] = adminSentinel,
            ["Observer:Lat"] = latSentinel,
            ["Observer:Lng"] = "34.8",
            ["Observer:AltMeters"] = "100",
            ["Observer:MinElevationDeg"] = "5"
        });

        var exitCode = await HostRunner.RunAsync(host);

        Assert.Equal(HostRunner.ConfigurationErrorExitCode, exitCode);
        Assert.Equal(1, exitCode);

        var critical = Assert.Single(logs.Entries, e => e.Level == LogLevel.Critical);
        Assert.Equal(HostRunner.LoggerCategory, critical.Category);
        Assert.Contains("N2YO:ApiKey is required", critical.Message);
        Assert.Contains("Firebase:ServiceAccountJson is required", critical.Message);
        Assert.Contains("ConnectionStrings:DefaultConnection is required", critical.Message);
        Assert.Contains("Admin:ApiKey", critical.Message);
        Assert.Contains("Observer:Lat must be between -90 and 90", critical.Message);
        // The DataAnnotations wrapper text is stripped down to the message naming the key.
        Assert.DoesNotContain("DataAnnotation validation failed", critical.Message);
        Assert.DoesNotContain(adminSentinel, critical.Message);
        Assert.DoesNotContain(latSentinel, critical.Message);

        // The host never got far enough to log "Hosting failed to start" with stack traces.
        Assert.DoesNotContain(logs.Entries, e => e.Level >= LogLevel.Error && e.Level != LogLevel.Critical);
        Assert.DoesNotContain(logs.Entries, e => e.Message.Contains("Hosting failed to start"));
    }

    [Fact]
    public async Task OtherStartupException_PropagatesUnchanged()
    {
        using var host = BuildValidHost(services => services.AddHostedService<ThrowingHostedService>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => HostRunner.RunAsync(host));

        Assert.Equal(ThrowingHostedService.Message, ex.Message);
    }

    [Fact]
    public async Task ValidConfiguration_RunsUntilShutdown_AndReturns0()
    {
        using var host = BuildValidHost(services => services.AddHostedService<StopImmediatelyHostedService>());

        var exitCode = await HostRunner.RunAsync(host).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task UnconvertibleValue_IsAConfigurationFailure_NamingTheKeyNotTheValue()
    {
        const string sentinel = "not-a-number-sentinel";
        var logs = new CapturingLoggerProvider();
        using var host = BuildHost(logs, new() { ["RateLimiting:Register:PermitLimit"] = sentinel });

        var exitCode = await HostRunner.RunAsync(host);

        Assert.Equal(1, exitCode);
        var critical = Assert.Single(logs.Entries, e => e.Level == LogLevel.Critical);
        Assert.Contains("RateLimiting:Register:PermitLimit", critical.Message);
        Assert.DoesNotContain(sentinel, critical.Message);
    }

    private static IHost BuildHost(CapturingLoggerProvider logs, Dictionary<string, string?> settings)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Testing",
            DisableDefaults = true
        });
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Logging.ClearProviders().AddProvider(logs);
        builder.Services.AddValidatedConfiguration();
        return builder.Build();
    }

    private static IHost BuildValidHost(Action<IServiceCollection> configure)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Testing",
            DisableDefaults = true
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Observer:Lat"] = "32",
            ["Observer:Lng"] = "34.8",
            ["Observer:AltMeters"] = "100",
            ["Observer:MinElevationDeg"] = "5",
            ["RateLimiting:Tester:TokenLimit"] = "60",
            ["RateLimiting:Tester:TokensPerPeriod"] = "1",
            ["RateLimiting:Tester:ReplenishmentPeriodSeconds"] = "1",
            ["RateLimiting:Anonymous:TokenLimit"] = "120",
            ["RateLimiting:Anonymous:TokensPerPeriod"] = "2",
            ["RateLimiting:Anonymous:ReplenishmentPeriodSeconds"] = "1",
            ["RateLimiting:Register:PermitLimit"] = "10",
            ["RateLimiting:Register:WindowSeconds"] = "3600"
        });
        builder.Configuration.AddInMemoryCollection(CustomWebApplicationFactory.TestSettings);
        builder.Logging.ClearProviders();
        builder.Services.AddValidatedConfiguration();
        configure(builder.Services);
        return builder.Build();
    }

    private sealed class ThrowingHostedService : IHostedService
    {
        public const string Message = "hosted service failed to start";
        public Task StartAsync(CancellationToken cancellationToken) => throw new InvalidOperationException(Message);
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StopImmediatelyHostedService(IHostApplicationLifetime lifetime) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            lifetime.ApplicationStarted.Register(lifetime.StopApplication);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

}
