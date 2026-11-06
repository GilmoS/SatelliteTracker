using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using SatelliteTracker.API.HealthChecks;
using SatelliteTracker.Database;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Security;
using SatelliteTracker.Tests.API.Infrastructure;
using Xunit;

namespace SatelliteTracker.Tests.API;

// The global limiter and the register policy through the real pipeline. Limits are lowered per test
// through configuration, with a one-hour replenishment so nothing refills mid-test.
public class RateLimitingTests : IDisposable
{
    private const string ClientIp = "203.0.113.10";
    private const string OtherClientIp = "203.0.113.20";
    private const string AnonymousEndpoint = "/api/satellites";
    private const string TesterEndpoint = "/api/settings/me";

    private readonly List<IDisposable> _disposables = [];

    public void Dispose()
    {
        foreach (var disposable in Enumerable.Reverse(_disposables)) disposable.Dispose();
    }

    // ── Authenticated testers ───────────────────────────────────────────────

    [Fact]
    public async Task TwoAuthenticatedTesters_HaveIndependentBuckets()
    {
        var (factory, client) = Create(Limits(testerTokens: 2, anonymousTokens: 1));
        var keyA = await SeedApiKeyAsync(factory);
        var keyB = await SeedApiKeyAsync(factory);

        // Same IP for both, and above the anonymous limit of 1: testers aren't counted per IP.
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, TesterEndpoint, ClientIp, keyA));
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, TesterEndpoint, ClientIp, keyA));
        await AssertRateLimitedAsync(await SendAsync(client, TesterEndpoint, ClientIp, keyA));

        Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, TesterEndpoint, ClientIp, keyB));
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, TesterEndpoint, ClientIp, keyB));
        await AssertRateLimitedAsync(await SendAsync(client, TesterEndpoint, ClientIp, keyB));

        // Tester traffic left the IP's anonymous bucket untouched.
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, AnonymousEndpoint, ClientIp));
    }

    [Fact]
    public async Task TesterBucket_FollowsTheTester_AcrossIps()
    {
        var (factory, client) = Create(Limits(testerTokens: 2));
        var key = await SeedApiKeyAsync(factory);

        Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, TesterEndpoint, ClientIp, key));
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, TesterEndpoint, OtherClientIp, key));
        await AssertRateLimitedAsync(await SendAsync(client, TesterEndpoint, "198.51.100.30", key));
    }

    // ── Unauthenticated: the fake-key rule ──────────────────────────────────

    [Fact]
    public async Task RandomInvalidKeys_ShareTheIpBucket_InsteadOfGettingFreshOnes()
    {
        var (factory, client) = Create(Limits(anonymousTokens: 3));
        var validKey = await SeedApiKeyAsync(factory);

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, AnonymousEndpoint, ClientIp, RandomKey()));

        // A brand-new fake key, and no key at all, both land in the same exhausted IP bucket.
        await AssertRateLimitedAsync(await SendAsync(client, AnonymousEndpoint, ClientIp, RandomKey()));
        await AssertRateLimitedAsync(await SendAsync(client, AnonymousEndpoint, ClientIp));

        // Another IP has its own bucket, and a real tester on the same IP is unaffected.
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, AnonymousEndpoint, OtherClientIp, RandomKey()));
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, AnonymousEndpoint, ClientIp, validKey));
    }

    [Fact]
    public async Task InactiveKey_IsTreatedAsUnauthenticated_AndSharesTheIpBucket()
    {
        var (factory, client) = Create(Limits(anonymousTokens: 1));
        var inactiveKey = await SeedApiKeyAsync(factory, isActive: false);

        Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, AnonymousEndpoint, ClientIp));
        await AssertRateLimitedAsync(await SendAsync(client, AnonymousEndpoint, ClientIp, inactiveKey));
    }

    [Fact]
    public async Task BehindTheProxy_ClientsArePartitionedByForwardedIp_NotByTheProxyAddress()
    {
        const string proxy = "10.0.0.5";
        var settings = Limits(anonymousTokens: 1);
        settings["ForwardedHeaders:KnownProxies:0"] = proxy;
        var (_, client) = Create(settings);

        Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, AnonymousEndpoint, proxy, forwardedFor: ClientIp));
        await AssertRateLimitedAsync(await SendAsync(client, AnonymousEndpoint, proxy, forwardedFor: ClientIp));
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, AnonymousEndpoint, proxy, forwardedFor: OtherClientIp));
    }

    [Fact]
    public async Task WithNoProxyConfigured_RotatingXForwardedFor_DoesNotEscapeTheIpBucket()
    {
        // Regression: with an empty trust list the framework would otherwise trust every peer.
        var (_, client) = Create(Limits(anonymousTokens: 1));

        Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, AnonymousEndpoint, ClientIp, forwardedFor: "198.51.100.1"));
        await AssertRateLimitedAsync(await SendAsync(client, AnonymousEndpoint, ClientIp, forwardedFor: "198.51.100.2"));
    }

    // ── POST /api/auth/register ─────────────────────────────────────────────

    [Fact]
    public async Task Register_EleventhRequestWithinAnHourFromOneIp_Returns429WithRetryAfter()
    {
        // Default limits from appsettings.json: 10 per hour.
        var (_, client) = Create();

        for (var i = 1; i <= 10; i++)
        {
            // Not allowlisted, so 403 — still counted.
            using var allowed = await RegisterAsync(client, ClientIp);
            Assert.Equal(HttpStatusCode.Forbidden, allowed.StatusCode);
        }

        using var rejected = await RegisterAsync(client, ClientIp);
        await AssertRateLimitedAsync(rejected);
        var retryAfter = int.Parse(rejected.Headers.GetValues("Retry-After").Single());
        Assert.InRange(retryAfter, 1, 3600);

        using var otherIp = await RegisterAsync(client, OtherClientIp);
        Assert.Equal(HttpStatusCode.Forbidden, otherIp.StatusCode);
    }

    [Fact]
    public async Task Register_RandomKeysDoNotEscapeTheIpWindow()
    {
        var settings = Limits();
        settings["RateLimiting:Register:PermitLimit"] = "2";
        var (_, client) = Create(settings);

        for (var i = 0; i < 2; i++)
        {
            using var allowed = await RegisterAsync(client, ClientIp, RandomKey());
            Assert.Equal(HttpStatusCode.Forbidden, allowed.StatusCode);
        }
        await AssertRateLimitedAsync(await RegisterAsync(client, ClientIp, RandomKey()));
    }

    // ── Health endpoints ────────────────────────────────────────────────────

    [Fact]
    public async Task HealthLiveAndReady_AreNeverRateLimited_AndUseNoTokens()
    {
        var (_, client) = Create(Limits(anonymousTokens: 1), FakeHealthProbes);

        for (var i = 0; i < 25; i++)
        {
            Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, "/health/live", ClientIp));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, await StatusAsync(client, "/health/ready", ClientIp));
        }

        // The IP's single token is still there, and the limiter is active.
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, AnonymousEndpoint, ClientIp));
        await AssertRateLimitedAsync(await SendAsync(client, AnonymousEndpoint, ClientIp));
    }

    [Fact]
    public async Task HealthDetails_IsRateLimited()
    {
        var (_, client) = Create(Limits(anonymousTokens: 1), FakeHealthProbes);

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(client, "/health/details", ClientIp));
        await AssertRateLimitedAsync(await SendAsync(client, "/health/details", ClientIp));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    // Starts from the appsettings.json defaults; only the given limits change, with no refill.
    private static Dictionary<string, string?> Limits(int? testerTokens = null, int? anonymousTokens = null)
    {
        var settings = new Dictionary<string, string?>();
        if (testerTokens is { } tester)
        {
            settings["RateLimiting:Tester:TokenLimit"] = tester.ToString();
            settings["RateLimiting:Tester:ReplenishmentPeriodSeconds"] = "3600";
        }
        if (anonymousTokens is { } anonymous)
        {
            settings["RateLimiting:Anonymous:TokenLimit"] = anonymous.ToString();
            settings["RateLimiting:Anonymous:ReplenishmentPeriodSeconds"] = "3600";
        }
        return settings;
    }

    private (WebApplicationFactory<Program> Factory, HttpClient Client) Create(
        Dictionary<string, string?>? settings = null, Action<IServiceCollection>? configureServices = null)
    {
        var baseFactory = new CustomWebApplicationFactory(settings);
        var factory = baseFactory.WithTestPeer(configureServices);
        var client = factory.CreateClient();
        _disposables.Add(baseFactory);
        _disposables.Add(factory);
        _disposables.Add(client);
        return (factory, client);
    }

    // The suite has no Postgres and must never reach Google.
    private static void FakeHealthProbes(IServiceCollection services)
    {
        services.RemoveAll<IHealthDatabaseProbe>();
        services.RemoveAll<IFcmTokenProbe>();
        services.AddSingleton(new Mock<IHealthDatabaseProbe>().Object);
        services.AddSingleton(new Mock<IFcmTokenProbe>().Object);
    }

    private static async Task<string> SeedApiKeyAsync(WebApplicationFactory<Program> factory, bool isActive = true)
    {
        var rawKey = RandomKey();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.ApiKeys.Add(new ApiKey
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@iai.co.il",
            DisplayName = "Tester",
            KeyHash = ApiKeyHasher.Hash(rawKey),
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
        return rawKey;
    }

    private static string RandomKey() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, string path, string peer, string? apiKey = null, string? forwardedFor = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path).From(peer);
        if (apiKey is not null) request.Headers.Add("X-Api-Key", apiKey);
        if (forwardedFor is not null) request.Headers.Add("X-Forwarded-For", forwardedFor);
        return await client.SendAsync(request);
    }

    private static async Task<HttpStatusCode> StatusAsync(
        HttpClient client, string path, string peer, string? apiKey = null, string? forwardedFor = null)
    {
        using var response = await SendAsync(client, path, peer, apiKey, forwardedFor);
        return response.StatusCode;
    }

    private static async Task<HttpResponseMessage> RegisterAsync(HttpClient client, string peer, string? apiKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(new { email = "not-allowlisted@example.com", displayName = "Someone" })
        }.From(peer);
        if (apiKey is not null) request.Headers.Add("X-Api-Key", apiKey);
        return await client.SendAsync(request);
    }

    private static async Task AssertRateLimitedAsync(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            Assert.True(response.Headers.Contains("Retry-After"), "429 without Retry-After");
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("""{"error":"rate_limited"}""", await response.Content.ReadAsStringAsync());
        }
    }
}
