using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SatelliteTracker.API.DTOs;
using SatelliteTracker.Database;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Security;
using SatelliteTracker.Tests.API.Infrastructure;
using Xunit;

namespace SatelliteTracker.Tests.API;

// HTTP-pipeline-level tests for GET/PUT /api/settings/me (Milestone E, Step 1.4), run through the
// real host via CustomWebApplicationFactory — same pattern as AuthenticationTests, since this is
// the first endpoint set where the GET itself is [Authorize]d and must be exercised through the
// real pipeline, not a hand-built ControllerContext.
public class SettingsMeEndpointTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SettingsMeEndpointTests()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<ApiKey> SeedApiKeyAsync(string rawKey, string email)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = "Tester",
            KeyHash = ApiKeyHasher.Hash(rawKey),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        context.ApiKeys.Add(apiKey);
        await context.SaveChangesAsync();
        return apiKey;
    }

    private static HttpRequestMessage BuildRequest<TBody>(HttpMethod method, string url, string? apiKey, TBody? body = default)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (apiKey is not null) request.Headers.Add("X-Api-Key", apiKey);
        return request;
    }

    [Fact]
    public async Task Get_WithoutAuth_Returns401()
    {
        var response = await _client.SendAsync(BuildRequest<object>(HttpMethod.Get, "/api/settings/me", null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_NoExistingRow_ReturnsDefaultAndCreatesNoRow()
    {
        var apiKey = await SeedApiKeyAsync("get-default-key", "get-default@iai.co.il");

        var response = await _client.SendAsync(BuildRequest<object>(HttpMethod.Get, "/api/settings/me", "get-default-key"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<UserSettingsDto>();
        Assert.NotNull(dto);
        Assert.Null(dto!.FcmToken);
        Assert.Empty(dto.AlertMinutes);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await context.UserSettings.AnyAsync(s => s.ApiKeyId == apiKey.Id));
    }

    [Fact]
    public async Task PutAlertMinutes_WithoutAuth_Returns401()
    {
        var response = await _client.SendAsync(
            BuildRequest(HttpMethod.Put, "/api/settings/me", null, new UpdateAlertMinutesRequest([5, 10])));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PutAlertMinutes_NoExistingRow_CreatesRowWithNullFcmToken()
    {
        var apiKey = await SeedApiKeyAsync("alert-create-key", "alert-create@iai.co.il");

        var response = await _client.SendAsync(
            BuildRequest(HttpMethod.Put, "/api/settings/me", "alert-create-key", new UpdateAlertMinutesRequest([5, 30])));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<UserSettingsDto>();
        Assert.Equal(new[] { 5, 30 }, dto!.AlertMinutes);
        Assert.Null(dto.FcmToken);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await context.UserSettings.SingleAsync(s => s.ApiKeyId == apiKey.Id);
        Assert.Null(row.FcmToken);
    }

    [Fact]
    public async Task PutAlertMinutes_ExistingFcmToken_UpdatesAlertMinutesLeavesFcmTokenUnchanged()
    {
        await SeedApiKeyAsync("alert-preserve-key", "alert-preserve@iai.co.il");

        var fcmResponse = await _client.SendAsync(
            BuildRequest(HttpMethod.Put, "/api/settings/me/fcm-token", "alert-preserve-key", new UpdateFcmTokenRequest("existing-token")));
        Assert.Equal(HttpStatusCode.OK, fcmResponse.StatusCode);

        var response = await _client.SendAsync(
            BuildRequest(HttpMethod.Put, "/api/settings/me", "alert-preserve-key", new UpdateAlertMinutesRequest([15, 60])));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<UserSettingsDto>();
        Assert.Equal(new[] { 15, 60 }, dto!.AlertMinutes);
        Assert.Equal("existing-token", dto.FcmToken); // critical regression check
    }

    [Fact]
    public async Task PutAlertMinutes_InvalidValue_Returns400()
    {
        await SeedApiKeyAsync("alert-invalid-key", "alert-invalid@iai.co.il");

        var response = await _client.SendAsync(
            BuildRequest(HttpMethod.Put, "/api/settings/me", "alert-invalid-key", new UpdateAlertMinutesRequest([45])));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutAlertMinutes_EmptyArray_IsValid()
    {
        await SeedApiKeyAsync("alert-empty-key", "alert-empty@iai.co.il");

        var response = await _client.SendAsync(
            BuildRequest(HttpMethod.Put, "/api/settings/me", "alert-empty-key", new UpdateAlertMinutesRequest([])));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<UserSettingsDto>();
        Assert.Empty(dto!.AlertMinutes);
    }

    [Fact]
    public async Task PutFcmToken_WithoutAuth_Returns401()
    {
        var response = await _client.SendAsync(
            BuildRequest(HttpMethod.Put, "/api/settings/me/fcm-token", null, new UpdateFcmTokenRequest("tok")));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PutFcmToken_NoExistingRow_CreatesRowWithEmptyAlertMinutes()
    {
        var apiKey = await SeedApiKeyAsync("fcm-create-key", "fcm-create@iai.co.il");

        var response = await _client.SendAsync(
            BuildRequest(HttpMethod.Put, "/api/settings/me/fcm-token", "fcm-create-key", new UpdateFcmTokenRequest("new-token")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<UserSettingsDto>();
        Assert.Equal("new-token", dto!.FcmToken);
        Assert.Empty(dto.AlertMinutes);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await context.UserSettings.SingleAsync(s => s.ApiKeyId == apiKey.Id);
        Assert.Empty(row.AlertMinutes);
    }

    [Fact]
    public async Task PutFcmToken_ExistingAlertMinutes_UpdatesFcmTokenLeavesAlertMinutesUnchanged()
    {
        await SeedApiKeyAsync("fcm-preserve-key", "fcm-preserve@iai.co.il");

        var alertResponse = await _client.SendAsync(
            BuildRequest(HttpMethod.Put, "/api/settings/me", "fcm-preserve-key", new UpdateAlertMinutesRequest([5, 10])));
        Assert.Equal(HttpStatusCode.OK, alertResponse.StatusCode);

        var response = await _client.SendAsync(
            BuildRequest(HttpMethod.Put, "/api/settings/me/fcm-token", "fcm-preserve-key", new UpdateFcmTokenRequest("swapped-token")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<UserSettingsDto>();
        Assert.Equal("swapped-token", dto!.FcmToken);
        Assert.Equal(new[] { 5, 10 }, dto.AlertMinutes); // critical regression check
    }

    [Fact]
    public async Task PutFcmToken_WhitespaceToken_Returns400()
    {
        await SeedApiKeyAsync("fcm-whitespace-key", "fcm-whitespace@iai.co.il");

        var response = await _client.SendAsync(
            BuildRequest(HttpMethod.Put, "/api/settings/me/fcm-token", "fcm-whitespace-key", new UpdateFcmTokenRequest("   ")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MultiTesterIsolation_SettingsDoNotInterfere()
    {
        await SeedApiKeyAsync("iso-key-1", "iso1@iai.co.il");
        await SeedApiKeyAsync("iso-key-2", "iso2@iai.co.il");

        await _client.SendAsync(
            BuildRequest(HttpMethod.Put, "/api/settings/me", "iso-key-1", new UpdateAlertMinutesRequest([5])));
        await _client.SendAsync(
            BuildRequest(HttpMethod.Put, "/api/settings/me/fcm-token", "iso-key-1", new UpdateFcmTokenRequest("token-1")));

        await _client.SendAsync(
            BuildRequest(HttpMethod.Put, "/api/settings/me", "iso-key-2", new UpdateAlertMinutesRequest([60])));
        await _client.SendAsync(
            BuildRequest(HttpMethod.Put, "/api/settings/me/fcm-token", "iso-key-2", new UpdateFcmTokenRequest("token-2")));

        var get1 = await (await _client.SendAsync(
            BuildRequest<object>(HttpMethod.Get, "/api/settings/me", "iso-key-1"))).Content.ReadFromJsonAsync<UserSettingsDto>();
        var get2 = await (await _client.SendAsync(
            BuildRequest<object>(HttpMethod.Get, "/api/settings/me", "iso-key-2"))).Content.ReadFromJsonAsync<UserSettingsDto>();

        Assert.Equal(new[] { 5 }, get1!.AlertMinutes);
        Assert.Equal("token-1", get1.FcmToken);
        Assert.Equal(new[] { 60 }, get2!.AlertMinutes);
        Assert.Equal("token-2", get2.FcmToken);
    }
}
