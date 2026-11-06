using System.Net;
using System.Net.Sockets;

namespace SatelliteTracker.API.Configuration;

// The "ForwardedHeaders" configuration section: the exact IP address(es) of the reverse proxy
// (Caddy) whose X-Forwarded-For / X-Forwarded-Proto headers are trusted. Empty by default, so a
// local `dotnet run` (no proxy) trusts nobody and uses the real TCP peer address. The Docker
// compose setup gives Caddy a fixed IP and sets it here, e.g. ForwardedHeaders__KnownProxies__0.
//
// Single addresses only, never ranges: anything else that can reach the API on the Docker network
// must not be able to claim an arbitrary client IP. See "Proxy & rate limiting" in CLAUDE.md.
public class TrustedProxyOptions
{
    public const string SectionName = "ForwardedHeaders";

    public List<string> KnownProxies { get; set; } = [];

    // Strict parse of one configured entry. IPAddress.TryParse alone also accepts shorthand such
    // as "10" (0.0.0.10) or "172.18.0" (172.18.0.0), and a bracketed "[::1]:80"; a typo like that
    // would silently trust the wrong address, so only a full dotted-quad IPv4 or a plain IPv6
    // address is accepted. CIDR ranges and host names fail.
    public static bool TryParseProxy(string? entry, out IPAddress? address)
    {
        address = null;
        if (string.IsNullOrWhiteSpace(entry)) return false;

        var value = entry.Trim();
        if (value.Contains('/') || value.Contains('[')) return false;
        if (!IPAddress.TryParse(value, out var parsed)) return false;
        if (parsed.AddressFamily == AddressFamily.InterNetwork && value.Count(c => c == '.') != 3) return false;

        address = parsed;
        return true;
    }
}
