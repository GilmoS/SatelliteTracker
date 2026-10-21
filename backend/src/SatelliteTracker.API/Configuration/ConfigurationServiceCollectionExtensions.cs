using Microsoft.Extensions.Options;
using SatelliteTracker.Database.Common;
using SatelliteTracker.TLEService.Client;

namespace SatelliteTracker.API.Configuration;

public static class ConfigurationServiceCollectionExtensions
{
    // Binds every required setting to a typed options class and validates it when the host starts,
    // so a missing or invalid setting stops the app instead of failing silently later. The startup
    // validator runs all of them before throwing, so one failed start lists every problem.
    // Messages name the setting key and the rule, never the value. Shape checks only: nothing here
    // connects to Postgres, Firebase or N2YO.
    //
    // Sections are bound through BindConfiguration (the final IConfiguration), never by reading
    // IConfiguration["..."], so a Key Vault provider can be added later without code changes.
    public static IServiceCollection AddValidatedConfiguration(this IServiceCollection services)
    {
        services.AddOptions<N2YoOptions>()
            .BindConfiguration(N2YoOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<FirebaseOptions>()
            .BindConfiguration(FirebaseOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<FirebaseOptions>, FirebaseOptionsValidator>();

        services.AddOptions<AdminOptions>()
            .BindConfiguration(AdminOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<ObserverSettings>()
            .BindConfiguration(ObserverSettings.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();

        return services;
    }
}
