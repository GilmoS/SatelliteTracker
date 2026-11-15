using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace SatelliteTracker.API.Configuration;

// Runs the host like app.Run(), but turns a startup configuration failure (I2.2's
// ValidateOnStart) into ONE critical log message listing the failed settings, and exit code 1,
// instead of the host's "Hosting failed to start" stack traces followed by an unhandled-exception
// crash with a runtime-specific exit code. Any exception from outside configuration validation
// keeps its default behavior.
public static partial class HostRunner
{
    public const int ConfigurationErrorExitCode = 1;
    public const string LoggerCategory = "SatelliteTracker.API.Startup";

    public static async Task<int> RunAsync(IHost host)
    {
        try
        {
            // The same validation Host.StartAsync runs first, run here beforehand so a failure
            // never reaches the host's own error logging. When it passes, StartAsync repeats it
            // against the already-validated (cached) options.
            if (!TryValidateConfiguration(host.Services, out var failures))
            {
                host.Services.GetRequiredService<ILoggerFactory>()
                    .CreateLogger(LoggerCategory)
                    .LogCritical(
                        "Startup aborted: {Count} configuration setting(s) missing or invalid. Fix them and restart:{Failures}",
                        failures.Count,
                        string.Concat(failures.Select(f => Environment.NewLine + "  - " + f)));
                return ConfigurationErrorExitCode;
            }

            await host.StartAsync();
            await host.WaitForShutdownAsync();
            return 0;
        }
        finally
        {
            // Disposing flushes the console logger, so the message above is written before exit.
            if (host is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
            else host.Dispose();
        }
    }

    // Runs every ValidateOnStart validator: the step that binds each options class from
    // configuration and validates it, nothing else. Whatever it throws is a configuration failure.
    // Exceptions from anywhere else (hosted services, the server) are not caught here and keep
    // their default behavior in RunAsync.
    public static bool TryValidateConfiguration(IServiceProvider services, out IReadOnlyList<string> failures)
    {
        try
        {
            services.GetService<IStartupValidator>()?.Validate();
            failures = [];
            return true;
        }
        catch (Exception ex)
        {
            failures = ConfigurationFailures(ex);
            return false;
        }
    }

    // The startup validator throws one OptionsValidationException when one options class fails,
    // and an AggregateException of them when several do; their failure messages name the setting
    // and the rule, never the value (see the validators). A value that can't be converted to the
    // property type ("ten" for an int) fails earlier, in the configuration binder, with an
    // InvalidOperationException whose message names the key and the target type but not the value
    // (.NET 8+). It stops the validator at that options class, so it is reported alone.
    public static IReadOnlyList<string> ConfigurationFailures(Exception ex) => ex switch
    {
        OptionsValidationException validation => validation.Failures.Select(Simplify).ToList(),
        AggregateException aggregate => aggregate.InnerExceptions.SelectMany(ConfigurationFailures).ToList(),
        _ => [ex.Message]
    };

    // "DataAnnotation validation failed for 'N2YoOptions' members: 'ApiKey' with the error:
    // 'N2YO:ApiKey is required and must not be empty.'." -> the quoted error, which already names
    // the configuration key. Messages from the custom validators are returned unchanged.
    private static string Simplify(string failure)
    {
        var match = DataAnnotationFailure().Match(failure);
        return match.Success ? match.Groups["error"].Value : failure;
    }

    [GeneratedRegex(@"^DataAnnotation validation failed for '[^']*' members: '[^']*' with the error: '(?<error>.*)'\.$", RegexOptions.Singleline)]
    private static partial Regex DataAnnotationFailure();
}
