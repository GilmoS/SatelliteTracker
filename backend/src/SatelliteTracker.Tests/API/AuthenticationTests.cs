using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.Database;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.Database.Security;
using SatelliteTracker.Tests.API.Infrastructure;
using Xunit;

namespace SatelliteTracker.Tests.API;

// HTTP-pipeline-level tests for the ApiKey authentication scheme, run through the real host via
// CustomWebApplicationFactory — the point is to verify the scheme actually works once wired into
// Program.cs (header parsing, DI, middleware ordering), not just HandleAuthenticateAsync in
// isolation. Repository-level behavior (GetByHashAsync, PassSubscription upsert/delete) has its
// own unit tests under Tests/Database.
public class AuthenticationTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthenticationTests()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<ApiKey> SeedApiKeyAsync(bool isActive, string rawKey)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@iai.co.il",
            DisplayName = "Tester",
            KeyHash = ApiKeyHasher.Hash(rawKey),
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow
        };
        context.ApiKeys.Add(apiKey);
        await context.SaveChangesAsync();
        return apiKey;
    }

    private async Task<Pass> SeedPassAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var sat = new Satellite
        {
            Id = Guid.NewGuid(),
            Name = "TEST-SAT",
            NoradId = 25544,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var tle = new TleRecord
        {
            Id = Guid.NewGuid(),
            SatelliteId = sat.Id,
            Line1 = "1 25544U 98067A   23001.00000000  .00000000  00000-0  00000-0 0  9999",
            Line2 = "2 25544  51.6400 208.9163 0001382  95.6617 344.7248 15.49309522223145",
            Epoch = DateTime.UtcNow,
            FetchedAt = DateTime.UtcNow
        };
        var pass = new Pass
        {
            Id = Guid.NewGuid(),
            SatelliteId = sat.Id,
            TleId = tle.Id,
            Aos = DateTime.UtcNow.AddHours(1),
            Los = DateTime.UtcNow.AddHours(1).AddMinutes(10),
            MaxElevation = 45,
            AosAzimuth = 90,
            LosAzimuth = 270,
            DurationSec = 600,
            CalculatedAt = DateTime.UtcNow
        };
        context.Satellites.Add(sat);
        context.TleRecords.Add(tle);
        context.Passes.Add(pass);
        await context.SaveChangesAsync();
        return pass;
    }

    private static HttpRequestMessage BuildRequest<TBody>(HttpMethod method, string url, string? apiKey, TBody body)
    {
        var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        if (apiKey is not null) request.Headers.Add("X-Api-Key", apiKey);
        return request;
    }

    [Fact]
    public async Task ProtectedEndpoint_NoHeader_Returns401_AnonymousGetOnSameControllerStillSucceeds()
    {
        var postResponse = await _client.SendAsync(
            BuildRequest(HttpMethod.Post, "/api/satellites", null, new CreateSatelliteRequest { Name = "X", NoradId = 1 }));
        Assert.Equal(HttpStatusCode.Unauthorized, postResponse.StatusCode);

        var getResponse = await _client.GetAsync("/api/satellites");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WellFormedButUnknownKey_Returns401WithSameBodyAsMissingHeader()
    {
        var missingHeaderResponse = await _client.SendAsync(
            BuildRequest(HttpMethod.Post, "/api/satellites", null, new CreateSatelliteRequest { Name = "X", NoradId = 1 }));

        var unknownKeyResponse = await _client.SendAsync(
            BuildRequest(HttpMethod.Post, "/api/satellites", "does-not-exist", new CreateSatelliteRequest { Name = "X", NoradId = 1 }));

        Assert.Equal(HttpStatusCode.Unauthorized, unknownKeyResponse.StatusCode);
        Assert.Equal(
            await missingHeaderResponse.Content.ReadAsStringAsync(),
            await unknownKeyResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ProtectedEndpoint_InactiveKey_Returns401WithSameBodyAsMissingHeader()
    {
        await SeedApiKeyAsync(isActive: false, rawKey: "inactive-key");

        var missingHeaderResponse = await _client.SendAsync(
            BuildRequest(HttpMethod.Post, "/api/satellites", null, new CreateSatelliteRequest { Name = "X", NoradId = 1 }));

        var inactiveKeyResponse = await _client.SendAsync(
            BuildRequest(HttpMethod.Post, "/api/satellites", "inactive-key", new CreateSatelliteRequest { Name = "X", NoradId = 1 }));

        Assert.Equal(HttpStatusCode.Unauthorized, inactiveKeyResponse.StatusCode);
        Assert.Equal(
            await missingHeaderResponse.Content.ReadAsStringAsync(),
            await inactiveKeyResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ProtectedEndpoint_ValidActiveKey_SucceedsAndUpdatesLastUsedAt()
    {
        var apiKey = await SeedApiKeyAsync(isActive: true, rawKey: "valid-key");

        var response = await _client.SendAsync(
            BuildRequest(HttpMethod.Post, "/api/satellites", "valid-key", new CreateSatelliteRequest { Name = "X", NoradId = 999123 }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await context.ApiKeys.SingleAsync(a => a.Id == apiKey.Id);
        Assert.NotNull(updated.LastUsedAt);
    }

    [Fact]
    public async Task PatchNotify_WithoutAuth_Returns401()
    {
        var pass = await SeedPassAsync();

        var response = await _client.SendAsync(
            BuildRequest(HttpMethod.Patch, $"/api/passes/{pass.Id}/notify", null, new PatchNotifyRequest(false)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PatchNotify_SetFalseThenTrue_TogglesEffectiveStatusAndIsolatesOtherTesters()
    {
        var pass = await SeedPassAsync();
        var apiKey1 = await SeedApiKeyAsync(isActive: true, rawKey: "tester1-key");
        var apiKey2 = await SeedApiKeyAsync(isActive: true, rawKey: "tester2-key");

        var falseResponse = await _client.SendAsync(
            BuildRequest(HttpMethod.Patch, $"/api/passes/{pass.Id}/notify", "tester1-key", new PatchNotifyRequest(false)));
        Assert.Equal(HttpStatusCode.OK, falseResponse.StatusCode);

        await AssertEffectiveNotify(pass.Id, apiKey1.Id, expected: false);
        await AssertEffectiveNotify(pass.Id, apiKey2.Id, expected: true); // multi-tester isolation

        var trueResponse = await _client.SendAsync(
            BuildRequest(HttpMethod.Patch, $"/api/passes/{pass.Id}/notify", "tester1-key", new PatchNotifyRequest(true)));
        Assert.Equal(HttpStatusCode.OK, trueResponse.StatusCode);

        await AssertEffectiveNotify(pass.Id, apiKey1.Id, expected: true);
    }

    private async Task AssertEffectiveNotify(Guid passId, Guid apiKeyId, bool expected)
    {
        using var scope = _factory.Services.CreateScope();
        var subscriptionRepo = scope.ServiceProvider.GetRequiredService<IPassSubscriptionRepository>();
        var result = await subscriptionRepo.GetEffectiveNotifyStatusAsync(passId, apiKeyId);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CalendarSchedule_WithoutAuth_Returns401()
    {
        var pass = await SeedPassAsync();

        var response = await _client.SendAsync(
            BuildRequest(HttpMethod.Post, "/api/calendar/schedule", null, new ScheduleCalendarRequest([pass.Id], [10])));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
