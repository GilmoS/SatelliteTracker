namespace SatelliteTracker.API.Configuration;

public static class SecretFilesConfigurationExtensions
{
    // Where Docker Compose mounts `secrets:` inside a container.
    public const string DefaultDirectory = "/run/secrets";

    // Reads one setting per file: the file name is the key, with "__" standing for ":"
    // (N2YO__ApiKey -> N2YO:ApiKey), and the content is the value, minus one trailing newline.
    // Optional: when the directory doesn't exist (dotnet run, tests) nothing is added.
    //
    // Added after every other source, so a secret file wins over appsettings*.json, user-secrets,
    // environment variables and the command line. Only mount the files a service should read: the
    // API reads every file in the directory.
    public static IConfigurationBuilder AddSecretFiles(
        this IConfigurationBuilder builder, string directory = DefaultDirectory)
        => builder.AddKeyPerFile(directory, optional: true, reloadOnChange: false);
}
