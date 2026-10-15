using Microsoft.Extensions.DependencyInjection;
using SatelliteTracker.OutlookService.Abstractions;
using SatelliteTracker.OutlookService.Services;

namespace SatelliteTracker.OutlookService.Extensions;

/// <summary>
/// DI registration for the OutlookService module.
/// </summary>
public static class OutlookServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ICalendarSyncService"/>. Active implementation is the ICS MVP
    /// (<see cref="IcsCalendarSyncService"/>).
    /// </summary>
    public static IServiceCollection AddOutlookServiceModule(this IServiceCollection services)
    {
        // To swap to Microsoft Graph once IT admin consent is available, replace the line below with:
        // services.AddScoped<ICalendarSyncService, GraphCalendarSyncService>();
        services.AddScoped<ICalendarSyncService, IcsCalendarSyncService>();

        return services;
    }
}
