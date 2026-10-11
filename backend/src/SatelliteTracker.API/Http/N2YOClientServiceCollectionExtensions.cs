using SatelliteTracker.TLEService.Client;

namespace SatelliteTracker.API.Http;

public static class N2YOClientServiceCollectionExtensions
{
    // Registers the typed N2YO HTTP client (reads N2YO:ApiKey from configuration).
    // The default HttpClient loggers are removed because they log the full request URL, which
    // contains the API key; N2YOHttpClientLogger logs the same events with the key redacted.
    public static IHttpClientBuilder AddN2YOClient(this IServiceCollection services)
    {
        services.AddSingleton<N2YOHttpClientLogger>();

        return services.AddHttpClient<IN2YOClient, N2YOClient>()
            .RemoveAllLoggers()
            .AddLogger<N2YOHttpClientLogger>();
    }
}
