using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.KeyPerFile;
using Microsoft.Extensions.DependencyInjection;
using SatelliteTracker.API.Configuration;
using SatelliteTracker.Tests.API.Infrastructure;
using Xunit;

namespace SatelliteTracker.Tests.API;

// Docker Compose mounts each secret as a file in /run/secrets. The file name is the config key
// with "__" for ":", the content is the value.
public sealed class SecretFilesConfigurationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "sattrakk-secrets-" + Guid.NewGuid().ToString("N"));

    public SecretFilesConfigurationTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void FileName_MapsDoubleUnderscoreToSectionSeparator()
    {
        WriteSecret("N2YO__ApiKey", "n2yo-from-file");
        WriteSecret("ConnectionStrings__DefaultConnection", "Host=postgres;Database=db;Username=user");
        WriteSecret("Admin__ApiKey", "admin-from-file");
        WriteSecret("Firebase__ServiceAccountJson", "{\n  \"type\": \"service_account\"\n}");

        var config = new ConfigurationBuilder().AddSecretFiles(_directory).Build();

        Assert.Equal("n2yo-from-file", config["N2YO:ApiKey"]);
        Assert.Equal("Host=postgres;Database=db;Username=user", config.GetConnectionString("DefaultConnection"));
        Assert.Equal("admin-from-file", config["Admin:ApiKey"]);
        // Multi-line content is kept intact.
        Assert.Equal("{\n  \"type\": \"service_account\"\n}", config["Firebase:ServiceAccountJson"]);
    }

    [Fact]
    public void TrailingNewline_IsTrimmed()
    {
        // Files written with echo end in a newline; it must not become part of the key.
        WriteSecret("N2YO__ApiKey", "n2yo-from-file" + Environment.NewLine);

        var config = new ConfigurationBuilder().AddSecretFiles(_directory).Build();

        Assert.Equal("n2yo-from-file", config["N2YO:ApiKey"]);
    }

    [Fact]
    public void SecretFile_OverridesEarlierSources()
    {
        WriteSecret("N2YO__ApiKey", "n2yo-from-file");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["N2YO:ApiKey"] = "n2yo-from-appsettings",
                ["Admin:ApiKey"] = "admin-from-appsettings"
            })
            .AddSecretFiles(_directory)
            .Build();

        Assert.Equal("n2yo-from-file", config["N2YO:ApiKey"]);
        Assert.Equal("admin-from-appsettings", config["Admin:ApiKey"]); // no file: untouched
    }

    [Fact]
    public void MissingDirectory_IsIgnored()
    {
        var missing = Path.Combine(_directory, "does-not-exist");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["N2YO:ApiKey"] = "from-memory" })
            .AddSecretFiles(missing)
            .Build();

        Assert.Equal("from-memory", config["N2YO:ApiKey"]);
    }

    [Fact]
    public void RealHost_ReadsRunSecrets_AfterEveryAppsettingsFile()
    {
        using var factory = new CustomWebApplicationFactory();

        var root = (IConfigurationRoot)factory.Services.GetRequiredService<IConfiguration>();
        var providers = root.Providers.ToList();

        var secretFiles = providers.OfType<KeyPerFileConfigurationProvider>().Single();
        var lastJson = providers.FindLastIndex(p => p is JsonConfigurationProvider);
        Assert.True(lastJson >= 0, "appsettings.json should be loaded");
        Assert.True(providers.IndexOf(secretFiles) > lastJson, "secret files must be added after appsettings*.json so they override it");
    }

    private void WriteSecret(string name, string content) =>
        File.WriteAllText(Path.Combine(_directory, name), content);
}
