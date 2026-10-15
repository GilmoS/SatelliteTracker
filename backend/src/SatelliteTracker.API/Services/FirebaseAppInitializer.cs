using FirebaseAdmin;
using Microsoft.Extensions.Options;
using SatelliteTracker.API.Configuration;

namespace SatelliteTracker.API.Services;

// Creates the default FirebaseApp when the host starts, instead of lazily on the first
// notification job tick. Hosted services start after options validation (ValidateOnStart), so
// the credential JSON has already been checked here, and before the jobs registered after it in
// Program.cs. Creating the app parses the credential only; it makes no network call.
public sealed class FirebaseAppInitializer : IHostedService
{
    // FirebaseApp.DefaultInstance is process-wide; several hosts (e.g. parallel test factories)
    // can start at once, and a second Create would throw.
    private static readonly Lock CreateLock = new();

    private readonly IOptions<FirebaseOptions> _options;
    private readonly ILogger<FirebaseAppInitializer> _logger;

    public FirebaseAppInitializer(IOptions<FirebaseOptions> options, ILogger<FirebaseAppInitializer> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (CreateLock)
        {
            if (FirebaseApp.DefaultInstance is null)
            {
                var credential = FirebaseCredentialParser.Parse(_options.Value.ServiceAccountJson!);
                FirebaseApp.Create(new AppOptions { Credential = credential });
                _logger.LogInformation("Firebase initialized");
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
