using FirebaseAdmin;
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
