using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using SatelliteTracker.API.HealthChecks;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.Tests.API.Infrastructure;
using Xunit;

namespace SatelliteTracker.Tests.API;

// The three health endpoints through the real host (CustomWebApplicationFactory), with the probes
// faked: the test suite has no Postgres, and must never reach Google. The real probes are covered by
// HealthProbeImplementationTests.
public class HealthEndpointTests : IDisposable
{
    private const string AdminKeyHeader = "X-Admin-Key";
    private const string SecretInException = "Host=db;Password=hunter2";

    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    private readonly CustomWebApplicationFactory _factory = new();
    private readonly FakeDatabaseProbe _database = new();
    private readonly FakeFcmProbe _fcm = new();
    private readonly Mock<IApiKeyRepository> _apiKeys = new();
    private readonly HttpClient _client;

    public HealthEndpointTests()
    {
        _client = _factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHealthDatabaseProbe>();
                services.RemoveAll<IFcmTokenProbe>();
                services.RemoveAll<TimeProvider>();
                services.RemoveAll<IApiKeyRepository>();
                services.AddSingleton<IHealthDatabaseProbe>(_database);
                services.AddSingleton<IFcmTokenProbe>(_fcm);
                services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(Now)));
                services.AddSingleton(_apiKeys.Object);
            }))
            .CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ── /health/live ────────────────────────────────────────────────────────

    [Fact]
    public async Task Live_DatabaseProbeFails_Returns200()
    {
        _database.Ping = _ => throw new InvalidOperationException(SecretInException);

        var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Live_RunsNoChecks()
    {
        await _client.GetAsync("/health/live");

        Assert.Equal(0, _database.CallCount);
        Assert.Equal(0, _fcm.CallCount);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Probes_SentAnApiKey_NeverReachAuthentication(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Api-Key", "any-key");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _apiKeys.VerifyNoOtherCalls(); // the handler's lookup would go through the retry strategy
    }

    // ── /health/ready ───────────────────────────────────────────────────────

    [Fact]
    public async Task Ready_AllChecksPass_Returns200Healthy_NoStore()
    {
        var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Ready_DatabaseProbeFails_Returns503Unhealthy_WithOnlyTheStatusWord()
    {
        _database.Ping = _ => throw new InvalidOperationException(SecretInException);

        var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ready_DatabaseProbeHangsAndHonorsCancellation_Returns503AfterTheTimeout()
    {
        _database.Ping = ct => Task.Delay(Timeout.InfiniteTimeSpan, ct);

        await AssertAnswersUnhealthyWithinTimeout();
    }

    [Fact]
    public async Task Ready_DatabaseProbeHangsAndIgnoresCancellation_Returns503AfterTheTimeout()
    {
        var neverCompletes = new TaskCompletionSource();
        _database.Ping = _ => neverCompletes.Task;

        await AssertAnswersUnhealthyWithinTimeout();
    }

    private async Task AssertAnswersUnhealthyWithinTimeout()
    {
        var stopwatch = Stopwatch.StartNew();

        var response = await _client.GetAsync("/health/ready");

        stopwatch.Stop();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(2.9), TimeSpan.FromSeconds(4.5));
    }

    [Fact]
    public async Task Ready_TleOlderThanThreshold_Returns200Degraded()
    {
        _database.TleFetches = [new("EROS C3", Now - HealthCheckPolicy.TleMaxAge)];

        await AssertReadyDegraded();
    }

    [Fact]
    public async Task Ready_OneActiveSatelliteHasNoTle_Returns200Degraded()
    {
        _database.TleFetches = [new("EROS C3", Now), new("RUNNER 1", null)];

        await AssertReadyDegraded();
    }

    [Fact]
    public async Task Ready_PassCalculationOlderThanThreshold_Returns200Degraded()
    {
        _database.LatestPassCalculation = Now - HealthCheckPolicy.PassMaxAge;

        await AssertReadyDegraded();
    }

    [Fact]
    public async Task Ready_NoPassesCalculated_Returns200Degraded()
    {
        _database.LatestPassCalculation = null;

        await AssertReadyDegraded();
    }

    [Fact]
    public async Task Ready_FcmTokenCheckFails_Returns200Degraded()
    {
        _fcm.RequestToken = _ => throw new InvalidOperationException(SecretInException);

        await AssertReadyDegraded();
    }

    private async Task AssertReadyDegraded()
    {
        var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Degraded", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ready_JustInsideBothFreshnessThresholds_ReturnsHealthy()
    {
        var justInside = TimeSpan.FromMinutes(1);
        _database.TleFetches = [new("EROS C3", Now - HealthCheckPolicy.TleMaxAge + justInside)];
        _database.LatestPassCalculation = Now - HealthCheckPolicy.PassMaxAge + justInside;

        var response = await _client.GetAsync("/health/ready");

        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ready_DatabaseUnhealthyOutranksDegradedChecks()
    {
        _database.Ping = _ => throw new InvalidOperationException();
        _fcm.RequestToken = _ => throw new InvalidOperationException();

        var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public void FreshnessThresholds_AreTheDecidedValues()
    {
        Assert.Equal(TimeSpan.FromHours(5), HealthCheckPolicy.TleMaxAge);
        Assert.Equal(TimeSpan.FromHours(3), HealthCheckPolicy.PassMaxAge);
        Assert.Equal(TimeSpan.FromSeconds(3), HealthCheckPolicy.CheckTimeout);
    }

    // ── /health/details ─────────────────────────────────────────────────────

    [Fact]
    public async Task Details_WithoutAdminKey_Returns401_AndRunsNoChecks()
    {
        var response = await _client.GetAsync("/health/details");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, _database.CallCount);
    }

    [Fact]
    public async Task Details_WithWrongAdminKey_Returns401()
    {
        var response = await SendDetailsAsync("not-the-admin-key");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Details_AllHealthy_ListsEveryCheckWithStatusDurationAndReason()
    {
        var response = await SendDetailsAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());

        var checks = body.RootElement.GetProperty("checks").EnumerateArray().ToList();
        Assert.Equal(
            ["database", "fcm", "pass-freshness", "tle-freshness"],
            checks.Select(c => c.GetProperty("name").GetString()).Order());

        foreach (var check in checks)
        {
            Assert.Equal("Healthy", check.GetProperty("status").GetString());
            Assert.True(check.GetProperty("durationMs").GetDouble() >= 0);
            Assert.False(string.IsNullOrEmpty(check.GetProperty("reason").GetString()));
        }
    }

    [Fact]
    public async Task Details_StaleTle_ReasonNamesTheSatelliteAndAge()
    {
        _database.TleFetches = [new("EROS C3", Now.AddHours(-7)), new("RUNNER 1", Now)];

        var response = await SendDetailsAsync();

        var tle = await ReadCheckAsync(response, "tle-freshness");
        Assert.Equal("Degraded", tle.Status);
        Assert.Equal("latest TLE for EROS C3 is 7h old", tle.Reason);
    }

    [Fact]
    public async Task Details_RecentPassCalculation_ReasonStatesTheAgeInMinutes()
    {
        _database.LatestPassCalculation = Now.AddMinutes(-45);

        var response = await SendDetailsAsync();

        var passes = await ReadCheckAsync(response, "pass-freshness");
        Assert.Equal("Healthy", passes.Status);
        Assert.Equal("latest pass calculation is 45m old", passes.Reason);
    }

    [Fact]
    public async Task Details_EveryCheckThrows_ContainsNoExceptionTextOrSecrets()
    {
        var failure = new InvalidOperationException($"boom {SecretInException}");
        _database.Ping = _ => throw failure;
        _database.GetTleFetches = _ => throw failure;
        _database.GetLatestPassCalculation = _ => throw failure;
        _fcm.RequestToken = _ => throw failure;

        var response = await SendDetailsAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("database probe failed", body);
        Assert.DoesNotContain("boom", body);
        Assert.DoesNotContain("hunter2", body);
        Assert.DoesNotContain("Password", body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain(CustomWebApplicationFactory.TestSettings["ConnectionStrings:DefaultConnection"]!, body);
    }

    [Fact]
    public async Task Details_ProbeTimesOut_ReasonIsOursNotTheFrameworks()
    {
        _database.Ping = ct => Task.Delay(Timeout.InfiniteTimeSpan, ct);

        var response = await SendDetailsAsync();

        var database = await ReadCheckAsync(response, "database");
        Assert.Equal("Unhealthy", database.Status);
        Assert.Equal("database probe timed out", database.Reason);
    }

    private async Task<HttpResponseMessage> SendDetailsAsync(string? adminKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/details");
        request.Headers.Add(AdminKeyHeader, adminKey ?? CustomWebApplicationFactory.TestSettings["Admin:ApiKey"]);
        return await _client.SendAsync(request);
    }

    private static async Task<(string? Status, string? Reason)> ReadCheckAsync(HttpResponseMessage response, string name)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var check = body.RootElement.GetProperty("checks").EnumerateArray()
            .Single(c => c.GetProperty("name").GetString() == name);
        return (check.GetProperty("status").GetString(), check.GetProperty("reason").GetString());
    }

    // Healthy by default; a test breaks one call at a time.
    private sealed class FakeDatabaseProbe : IHealthDatabaseProbe
    {
        private int _callCount;

        public int CallCount => _callCount;
        public Func<CancellationToken, Task> Ping { get; set; } = _ => Task.CompletedTask;
        public IReadOnlyList<SatelliteTleFetch> TleFetches { get; set; } = [new("EROS C3", Now), new("RUNNER 1", Now)];
        public Func<CancellationToken, Task<IReadOnlyList<SatelliteTleFetch>>>? GetTleFetches { get; set; }
        public DateTime? LatestPassCalculation { get; set; } = Now;
        public Func<CancellationToken, Task<DateTime?>>? GetLatestPassCalculation { get; set; }

        public Task PingAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref _callCount);
            return Ping(ct);
        }

        public Task<IReadOnlyList<SatelliteTleFetch>> GetLatestTleFetchPerActiveSatelliteAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref _callCount);
            return GetTleFetches?.Invoke(ct) ?? Task.FromResult(TleFetches);
        }

        public Task<DateTime?> GetLatestPassCalculationAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref _callCount);
            return GetLatestPassCalculation?.Invoke(ct) ?? Task.FromResult(LatestPassCalculation);
        }
    }

    private sealed class FakeFcmProbe : IFcmTokenProbe
    {
        private int _callCount;

        public int CallCount => _callCount;
        public Func<CancellationToken, Task> RequestToken { get; set; } = _ => Task.CompletedTask;

        public Task RequestTokenAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref _callCount);
            return RequestToken(ct);
        }
    }
}
