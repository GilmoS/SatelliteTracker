using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SatelliteTracker.Tests.API.Infrastructure;
using Xunit;

namespace SatelliteTracker.Tests.API;

// X-Forwarded-For / X-Forwarded-Proto are applied only when the TCP peer is an exact
// ForwardedHeaders:KnownProxies address. Runs through the real pipeline; the peer address is set by
// TestPeerStartupFilter and the result read back from ConnectionEchoController.
public class ForwardedHeadersTests : IDisposable
{
    private const string Proxy = "10.0.0.5";

    private readonly CustomWebApplicationFactory _factory = new(new Dictionary<string, string?>
    {
        ["ForwardedHeaders:KnownProxies:0"] = Proxy
    });
    private readonly CustomWebApplicationFactory _noProxyFactory = new();

    public void Dispose()
    {
        _factory.Dispose();
        _noProxyFactory.Dispose();
    }

    [Fact]
    public async Task FromConfiguredProxy_RemoteIpAndSchemeComeFromHeaders()
    {
        var echo = await EchoAsync(_factory, peer: Proxy, forwardedFor: "203.0.113.7", forwardedProto: "https");

        Assert.Equal("203.0.113.7", echo.RemoteIp);
        Assert.Equal("https", echo.Scheme);
    }

    [Fact]
    public async Task FromConfiguredProxy_OnlyTheRightMostEntryIsUsed()
    {
        // The left entry is whatever the client sent Caddy; only the one Caddy appended counts.
        var echo = await EchoAsync(_factory, peer: Proxy, forwardedFor: "198.51.100.66, 203.0.113.7");

        Assert.Equal("203.0.113.7", echo.RemoteIp);
    }

    [Fact]
    public async Task FromConfiguredProxyAsIPv4MappedIPv6_IsStillTrusted()
    {
        var echo = await EchoAsync(_factory, peer: $"::ffff:{Proxy}", forwardedFor: "203.0.113.7");

        Assert.Equal("203.0.113.7", echo.RemoteIp);
    }

    [Theory]
    [InlineData("10.0.0.6")]        // a neighbor on the same network
    [InlineData("127.0.0.1")]       // loopback, trusted by the framework's defaults, which are cleared
    [InlineData("203.0.113.99")]    // a client reaching the API directly
    public async Task FromAnyOtherPeer_HeadersAreIgnored(string peer)
    {
        var echo = await EchoAsync(_factory, peer: peer, forwardedFor: "198.51.100.1", forwardedProto: "https");

        Assert.Equal(peer, echo.RemoteIp);
        Assert.Equal("http", echo.Scheme);
    }

    [Fact]
    public async Task DefaultEmptyProxyList_TrustsNoOne_NotEvenLoopback()
    {
        var echo = await EchoAsync(_noProxyFactory, peer: "127.0.0.1", forwardedFor: "198.51.100.1", forwardedProto: "https");

        Assert.Equal("127.0.0.1", echo.RemoteIp);
        Assert.Equal("http", echo.Scheme);
    }

    private static async Task<ConnectionEcho> EchoAsync(
        WebApplicationFactory<Program> factory, string peer, string forwardedFor, string? forwardedProto = null)
    {
        using var client = factory.WithTestPeer().CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/__test/connection").From(peer);
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        if (forwardedProto is not null) request.Headers.Add("X-Forwarded-Proto", forwardedProto);

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ConnectionEcho>())!;
    }
}
