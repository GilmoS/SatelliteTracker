using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using SatelliteTracker.API.Configuration;

namespace SatelliteTracker.API.Http;

public static class ForwardedHeadersServiceCollectionExtensions
{
    // Configures UseForwardedHeaders() to trust X-Forwarded-For / X-Forwarded-Proto only from the
    // exact addresses in ForwardedHeaders:KnownProxies (validated at startup). The framework's
    // defaults (loopback in KnownNetworks/KnownProxies) are cleared, so with an empty list no
    // forwarded header is ever applied. A request from any other peer is still served normally;
    // its forwarded headers are just ignored and RemoteIpAddress stays the TCP peer.
    //
    // ForwardLimit = 1: only the right-most X-Forwarded-For entry, the one Caddy itself appended,
    // is used. Anything to its left was sent by the client and can be forged.
    public static IServiceCollection AddTrustedForwardedHeaders(this IServiceCollection services)
    {
        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<TrustedProxyOptions>>((options, proxies) =>
            {
                options.ForwardLimit = 1;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();

                foreach (var entry in proxies.Value.KnownProxies)
                {
                    if (TrustedProxyOptions.TryParseProxy(entry, out var address))
                        options.KnownProxies.Add(address!);
                }

                // With both KnownProxies and KnownNetworks empty, ForwardedHeadersMiddleware skips
                // the known-proxy check altogether and trusts the headers from EVERY peer. So an
                // empty list must turn header processing off, not leave it on with nothing trusted.
                options.ForwardedHeaders = options.KnownProxies.Count == 0
                    ? ForwardedHeaders.None
                    : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            });

        return services;
    }
}
