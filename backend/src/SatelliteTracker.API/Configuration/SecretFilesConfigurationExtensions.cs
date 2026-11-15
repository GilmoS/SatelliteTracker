using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

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
    //
    // A file that is listed but can't be read (no read permission for the app user, or a bind
    // mount whose source file is missing) would make the provider throw while configuration is
    // still being built, crashing the process before HostRunner runs. Such a file is skipped
    // instead and reported by a startup validator (SecretFilesValidator), so it ends like any
    // other configuration failure: one critical message and exit code 1.
    public static IHostApplicationBuilder AddSecretFiles(
        this IHostApplicationBuilder builder, string directory = DefaultDirectory)
    {
        var unreadable = new SortedSet<string>(StringComparer.Ordinal);
        builder.Configuration.AddSecretFiles(directory, name => { lock (unreadable) unreadable.Add(name); });

        builder.Services.AddOptions<SecretFilesReport>()
            .Configure(report =>
            {
                report.Directory = directory;
                lock (unreadable) report.UnreadableFiles = unreadable.ToList();
            })
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<SecretFilesReport>, SecretFilesValidator>();
        return builder;
    }

    // The configuration source alone. Unreadable files are skipped and passed to onUnreadable.
    public static IConfigurationBuilder AddSecretFiles(
        this IConfigurationBuilder builder, string directory = DefaultDirectory, Action<string>? onUnreadable = null)
        => builder.AddKeyPerFile(source =>
        {
            // The key-per-file provider opens every listed file before its IgnoreCondition is
            // consulted, so unreadable files are dropped from the listing itself.
            source.FileProvider = Directory.Exists(directory)
                ? new ReadableFilesOnlyProvider(new PhysicalFileProvider(directory), onUnreadable)
                : null;
            source.Optional = true;
            source.ReloadOnChange = false;
        });

    private sealed class ReadableFilesOnlyProvider(IFileProvider inner, Action<string>? onUnreadable) : IFileProvider
    {
        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            var contents = inner.GetDirectoryContents(subpath);
            return contents.Exists
                ? new FilteredContents(contents.Where(f => f.IsDirectory || CanRead(f)).ToList())
                : contents;
        }

        public IFileInfo GetFileInfo(string subpath) => inner.GetFileInfo(subpath);

        public IChangeToken Watch(string filter) => inner.Watch(filter);

        private bool CanRead(IFileInfo file)
        {
            try
            {
                using var _ = file.CreateReadStream();
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                onUnreadable?.Invoke(file.Name);
                return false;
            }
        }

        private sealed class FilteredContents(IReadOnlyList<IFileInfo> files) : IDirectoryContents
        {
            public bool Exists => true;
            public IEnumerator<IFileInfo> GetEnumerator() => files.GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}

// Secret files that were present but could not be read when configuration loaded.
public sealed class SecretFilesReport
{
    public string Directory { get; set; } = SecretFilesConfigurationExtensions.DefaultDirectory;
    public IReadOnlyList<string> UnreadableFiles { get; set; } = [];
}

public sealed class SecretFilesValidator : IValidateOptions<SecretFilesReport>
{
    // Names the file and the setting it would have supplied, never any content.
    public ValidateOptionsResult Validate(string? name, SecretFilesReport options)
        => options.UnreadableFiles.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(options.UnreadableFiles.Select(file =>
                $"Secret file {options.Directory.TrimEnd('/', '\\')}/{file} ({file.Replace("__", ":")}) exists but cannot be read."));
}
