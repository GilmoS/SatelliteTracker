using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace SatelliteTracker.Tests.API.Infrastructure;

// TestServer leaves Connection.RemoteIpAddress null, so every request would look like the same
// unknown client. This startup filter runs ahead of all of Program.cs's middleware (so before
// UseForwardedHeaders) and sets the TCP peer address from a test-only header, which it then
// removes. It plays the part of "who opened the TCP connection": Caddy, or a client reaching the
// API directly.
public sealed class TestPeerStartupFilter : IStartupFilter
{
    public const string PeerHeader = "X-Test-Peer-IP";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            if (context.Request.Headers.TryGetValue(PeerHeader, out var value)
                && IPAddress.TryParse(value.ToString(), out var address))
            {
                context.Connection.RemoteIpAddress = address;
            }

            context.Request.Headers.Remove(PeerHeader);
            return nextMiddleware(context);
        });
        next(app);
    };
}

// Echoes what the app saw after UseForwardedHeaders. Only added to the host by WithTestPeer.
[ApiController]
[Route("__test/connection")]
public sealed class ConnectionEchoController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new ConnectionEcho(HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Scheme));
}

public sealed record ConnectionEcho(string? RemoteIp, string Scheme);

public static class TestPeerExtensions
{
    // The factory with TestPeerStartupFilter and ConnectionEchoController added. Use the returned
    // factory's Services for seeding: it shares the base factory's SQLite connection.
    public static WebApplicationFactory<Program> WithTestPeer(
        this WebApplicationFactory<Program> factory, Action<IServiceCollection>? configureServices = null) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStartupFilter, TestPeerStartupFilter>();
            services.AddControllers().AddApplicationPart(typeof(ConnectionEchoController).Assembly);
            configureServices?.Invoke(services);
        }));

    public static HttpRequestMessage From(this HttpRequestMessage request, string peerIp)
    {
        request.Headers.Add(TestPeerStartupFilter.PeerHeader, peerIp);
        return request;
    }
}
