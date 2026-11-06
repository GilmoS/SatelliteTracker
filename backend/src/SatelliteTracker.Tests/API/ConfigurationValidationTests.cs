using FirebaseAdmin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SatelliteTracker.API.Configuration;
using SatelliteTracker.Tests.API.Infrastructure;
using Xunit;

namespace SatelliteTracker.Tests.API;

// Startup must fail, naming the setting and the broken rule, when a required setting is missing
// or invalid, and the failure must never echo a configured value. These boot the real host
// (Program.cs) through CustomWebApplicationFactory with one setting broken at a time.
public class ConfigurationValidationTests
{
    [Fact]
    public void ValidConfiguration_HostStarts_AndInitializesFirebase()
    {
        using var factory = new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        Assert.NotNull(FirebaseApp.DefaultInstance);
    }

    [Fact]
    public void MissingN2YoApiKey_FailsStartup()
    {
        var message = StartupFailure(new() { ["N2YO:ApiKey"] = null });

        Assert.Contains("N2YO:ApiKey is required", message);
        AssertNoConfiguredSecret(message);
    }

    [Fact]
    public void MissingObserverSection_FailsStartup_InsteadOfDefaultingToZero()
    {
        var message = StartupFailure(new()
        {
            ["Observer:Lat"] = null,
            ["Observer:Lng"] = null,
            ["Observer:AltMeters"] = null,
            ["Observer:MinElevationDeg"] = null
        });

        Assert.Contains("Observer:Lat is required", message);
        Assert.Contains("Observer:Lng is required", message);
        Assert.Contains("Observer:AltMeters is required", message);
        Assert.Contains("Observer:MinElevationDeg is required", message);
        AssertNoConfiguredSecret(message);
    }

    [Fact]
    public void ObserverLatOutOfRange_FailsStartup_WithoutEchoingValue()
    {
        const string outOfRange = "123.456789";

        var message = StartupFailure(new() { ["Observer:Lat"] = outOfRange });

        Assert.Contains("Observer:Lat must be between -90 and 90", message);
        Assert.DoesNotContain(outOfRange, message);
        AssertNoConfiguredSecret(message);
    }

    [Fact]
    public void MalformedFirebaseServiceAccountJson_FailsStartup_WithoutEchoingValue()
    {
        const string sentinel = "MALFORMED-SENTINEL-7f3a";
        var malformed = $"{{\"type\": \"service_account\", \"private_key\": \"{sentinel}\"";

        var message = StartupFailure(new() { ["Firebase:ServiceAccountJson"] = malformed });

        Assert.Contains("Firebase:ServiceAccountJson is not a valid service-account credential JSON", message);
        Assert.DoesNotContain(sentinel, message);
        AssertNoConfiguredSecret(message);
    }

    [Fact]
    public void MissingConnectionString_FailsStartup()
    {
        var message = StartupFailure(new() { ["ConnectionStrings:DefaultConnection"] = null });

        Assert.Contains("ConnectionStrings:DefaultConnection is required", message);
        AssertNoConfiguredSecret(message);
    }

    [Fact]
    public void SeveralInvalidSettings_AreAllReportedInOneStartupAttempt()
    {
        const string shortAdminKey = "too-short-admin-key";

        var message = StartupFailure(new()
        {
            ["N2YO:ApiKey"] = null,
            ["Admin:ApiKey"] = shortAdminKey,
            ["Observer:Lng"] = "200"
        });

        Assert.Contains("N2YO:ApiKey is required", message);
        Assert.Contains("Admin:ApiKey must be at least 32 characters long", message);
        Assert.Contains("Observer:Lng must be between -180 and 180", message);
        Assert.DoesNotContain(shortAdminKey, message);
        AssertNoConfiguredSecret(message);
    }

    // ── ForwardedHeaders and RateLimiting (I2.5) ────────────────────────────

    [Fact]
    public void InvalidKnownProxyEntries_FailStartup_NamingEachEntry()
    {
        var message = StartupFailure(new()
        {
            ["ForwardedHeaders:KnownProxies:0"] = "172.30.0.10",
            ["ForwardedHeaders:KnownProxies:1"] = "172.30.0.0/24",
            ["ForwardedHeaders:KnownProxies:2"] = "caddy"
        });

        Assert.Contains("ForwardedHeaders:KnownProxies:1 must be a single IP address", message);
        Assert.Contains("ForwardedHeaders:KnownProxies:2 must be a single IP address", message);
        Assert.DoesNotContain("ForwardedHeaders:KnownProxies:0", message);
        AssertNoConfiguredSecret(message);
    }

    [Fact]
    public void InvalidRateLimits_FailStartup_ListingEveryKey()
    {
        var message = StartupFailure(new()
        {
            ["RateLimiting:Tester:TokenLimit"] = "0",
            ["RateLimiting:Anonymous:TokensPerPeriod"] = "-5",
            ["RateLimiting:Register:WindowSeconds"] = null
        });

        Assert.Contains("RateLimiting:Tester:TokenLimit must be a positive integer", message);
        Assert.Contains("RateLimiting:Anonymous:TokensPerPeriod must be a positive integer", message);
        Assert.Contains("RateLimiting:Register:WindowSeconds is required", message);
        AssertNoConfiguredSecret(message);
    }

    [Fact]
    public void NonIntegerRateLimit_FailsStartup_NamingTheKey()
    {
        var message = StartupFailure(new() { ["RateLimiting:Register:PermitLimit"] = "ten" });

        Assert.Contains("RateLimiting:Register:PermitLimit", message);
        AssertNoConfiguredSecret(message);
    }

    [Fact]
    public void DefaultSettings_TrustNoProxy_AndUseTheDocumentedLimits()
    {
        using var factory = new CustomWebApplicationFactory();

        var proxies = factory.Services.GetRequiredService<IOptions<TrustedProxyOptions>>().Value;
        var limits = factory.Services.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

        Assert.Empty(proxies.KnownProxies);
        Assert.Equal((60, 60, 60), (limits.Tester.TokenLimit, limits.Tester.TokensPerPeriod, limits.Tester.ReplenishmentPeriodSeconds));
        Assert.Equal((120, 120, 60), (limits.Anonymous.TokenLimit, limits.Anonymous.TokensPerPeriod, limits.Anonymous.ReplenishmentPeriodSeconds));
        Assert.Equal((10, 3600), (limits.Register.PermitLimit, limits.Register.WindowSeconds));
    }

    [Theory]
    [InlineData("172.30.0.10", true)]
    [InlineData(" 172.30.0.10 ", true)]
    [InlineData("::1", true)]
    [InlineData("fd00:abcd::10", true)]
    [InlineData("", false)]
    [InlineData("10", false)]                 // IPAddress.TryParse reads this as 0.0.0.10
    [InlineData("172.30.0", false)]           // ...and this as 172.30.0.0
    [InlineData("172.30.0.0/24", false)]
    [InlineData("256.1.1.1", false)]
    [InlineData("10.0.0.1:80", false)]
    [InlineData("[::1]:80", false)]
    [InlineData("caddy", false)]
    public void TrustedProxyOptions_AcceptsOnlySingleAddresses(string entry, bool valid)
    {
        Assert.Equal(valid, TrustedProxyOptions.TryParseProxy(entry, out _));

        var result = new TrustedProxyOptionsValidator().Validate(null, new TrustedProxyOptions { KnownProxies = [entry] });
        Assert.Equal(valid, result.Succeeded);
    }

    [Fact]
    public void RateLimitingOptionsValidator_RequiresEveryValue()
    {
        var result = new RateLimitingOptionsValidator().Validate(null, new RateLimitingOptions());

        Assert.True(result.Failed);
        foreach (var key in new[]
                 {
                     "Tester:TokenLimit", "Tester:TokensPerPeriod", "Tester:ReplenishmentPeriodSeconds",
                     "Anonymous:TokenLimit", "Anonymous:TokensPerPeriod", "Anonymous:ReplenishmentPeriodSeconds",
                     "Register:PermitLimit", "Register:WindowSeconds"
                 })
        {
            Assert.Contains($"RateLimiting:{key} is required", result.FailureMessage);
        }
    }

    // ── Validators in isolation ─────────────────────────────────────────────

    [Theory]
    [InlineData("Host=localhost;Database=db;Username=user", null)]
    [InlineData("Database=db;Username=user", "must specify Host")]
    [InlineData("Host=localhost;Username=user", "must specify Database")]
    [InlineData("Host=localhost;Database=db", "must specify Username")]
    [InlineData("Host=localhost;NotAKeyword=SENTINEL-9c1d", "is not a valid PostgreSQL connection string")]
    public void DatabaseOptionsValidator_ChecksShape(string connectionString, string? expectedFailure)
    {
        var result = new DatabaseOptionsValidator().Validate(null, new DatabaseOptions { DefaultConnection = connectionString });

        if (expectedFailure is null)
        {
            Assert.True(result.Succeeded);
            return;
        }

        Assert.True(result.Failed);
        Assert.Contains(expectedFailure, result.FailureMessage);
        Assert.Contains("ConnectionStrings:DefaultConnection", result.FailureMessage);
        Assert.DoesNotContain("SENTINEL", result.FailureMessage);
    }

    private const string AzureSslRule = "must set SSL Mode=VerifyFull when Host is an Azure Database for PostgreSQL server";

    [Theory]
    [InlineData("Host=sattrakk-db.postgres.database.azure.com;Database=db;Username=user;SSL Mode=VerifyFull", true)]
    [InlineData("Host=SATTRAKK-DB.Postgres.Database.Azure.COM;Database=db;Username=user;SSL Mode=VerifyFull", true)]
    [InlineData("Host=sattrakk-db.postgres.database.azure.com;Database=db;Username=user;SSL Mode=Require", false)]
    [InlineData("Host=sattrakk-db.postgres.database.azure.com;Database=db;Username=user;SSL Mode=Prefer", false)]
    [InlineData("Host=sattrakk-db.postgres.database.azure.com;Database=db;Username=user;SSL Mode=Disable", false)]
    [InlineData("Host=sattrakk-db.postgres.database.azure.com;Database=db;Username=user;SSL Mode=VerifyCA", false)]
    [InlineData("Host=sattrakk-db.postgres.database.azure.com;Database=db;Username=user", false)]
    [InlineData("Host=SATTRAKK-DB.POSTGRES.DATABASE.AZURE.COM;Database=db;Username=user;SSL Mode=Disable", false)]
    [InlineData("Host=localhost,sattrakk-db.postgres.database.azure.com:5432;Database=db;Username=user;SSL Mode=Disable", false)]
    [InlineData("Host=localhost;Database=db;Username=user;SSL Mode=Disable", true)]
    [InlineData("Host=postgres;Database=db;Username=user;SSL Mode=Disable", true)]
    [InlineData("Host=postgres.database.azure.com.example.org;Database=db;Username=user;SSL Mode=Disable", true)]
    public void DatabaseOptionsValidator_AzureHostRequiresVerifyFull(string connectionString, bool valid)
    {
        var result = new DatabaseOptionsValidator().Validate(null, new DatabaseOptions { DefaultConnection = connectionString });

        if (valid)
        {
            Assert.True(result.Succeeded, result.FailureMessage);
            return;
        }

        Assert.True(result.Failed);
        Assert.Contains($"ConnectionStrings:DefaultConnection {AzureSslRule}", result.FailureMessage);
        Assert.DoesNotContain("sattrakk-db", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    // The recommended connection strings documented in CLAUDE.md ("Required configuration").
    [Theory]
    [InlineData("Host=localhost;Port=5435;Database=satellite_tracker;Username=satellite_user;Password=placeholder;SSL Mode=Disable;Timeout=15;Command Timeout=30;Keepalive=30;Maximum Pool Size=20")]
    [InlineData("Host=sattrakk-db.postgres.database.azure.com;Port=5432;Database=satellite_tracker;Username=satellite_user;Password=placeholder;SSL Mode=VerifyFull;Timeout=15;Command Timeout=30;Keepalive=30;Maximum Pool Size=20")]
    public void DatabaseOptionsValidator_AcceptsRecommendedConnectionStrings(string connectionString)
    {
        var result = new DatabaseOptionsValidator().Validate(null, new DatabaseOptions { DefaultConnection = connectionString });

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact]
    public void AzureHostWithoutVerifyFull_FailsStartup_WithoutEchoingValue()
    {
        const string connectionString =
            "Host=sattrakk-db.postgres.database.azure.com;Database=sattrakk;Username=sattrakk_app;Password=SENTINEL-PW-5e2b;SSL Mode=Disable";

        var message = StartupFailure(new() { ["ConnectionStrings:DefaultConnection"] = connectionString });

        Assert.Contains(AzureSslRule, message);
        Assert.DoesNotContain("SENTINEL", message);
        Assert.DoesNotContain("sattrakk-db", message);
        AssertNoConfiguredSecret(message);
    }

    [Fact]
    public void FirebaseOptionsValidator_RejectsNonServiceAccountCredential()
    {
        // Well-formed JSON for a different credential type (an OAuth user credential).
        const string userCredential = """
            {"type": "authorized_user", "client_id": "x", "client_secret": "SENTINEL-41be", "refresh_token": "y"}
            """;

        var result = new FirebaseOptionsValidator().Validate(null, new FirebaseOptions { ServiceAccountJson = userCredential });

        Assert.True(result.Failed);
        Assert.Contains("Firebase:ServiceAccountJson", result.FailureMessage);
        Assert.DoesNotContain("SENTINEL", result.FailureMessage);
    }

    [Fact]
    public void FirebaseOptionsValidator_AcceptsGeneratedServiceAccount()
    {
        var result = new FirebaseOptionsValidator().Validate(null, new FirebaseOptions { ServiceAccountJson = TestServiceAccount.CreateJson() });

        Assert.True(result.Succeeded);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    // Boots the host with the overrides applied and returns the full startup failure, inner
    // exceptions included, so the assertions see everything an operator would.
    private static string StartupFailure(Dictionary<string, string?> overrides)
    {
        using var factory = new CustomWebApplicationFactory(overrides);

        var ex = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(ex);
        return ex.ToString();
    }

    // None of the valid settings the factory configured may show up in a failure message.
    private static void AssertNoConfiguredSecret(string message)
    {
        foreach (var (key, value) in CustomWebApplicationFactory.TestSettings)
        {
            if (string.IsNullOrEmpty(value)) continue;
            Assert.False(message.Contains(value, StringComparison.Ordinal), $"Startup failure message contains the value of {key}.");
        }
    }
}
