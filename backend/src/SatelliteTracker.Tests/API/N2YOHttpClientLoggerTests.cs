using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SatelliteTracker.API.Http;
using SatelliteTracker.Tests.API.Infrastructure;
using SatelliteTracker.TLEService.Client;
using Xunit;

namespace SatelliteTracker.Tests.API;

public class N2YOHttpClientLoggerTests
{
    private const string SecretKey = "SECRET-N2YO-KEY-123";

    [Theory]
    [InlineData("https://api.n2yo.com/rest/v1/satellite/tle/25544&apiKey=abc123",
                "https://api.n2yo.com/rest/v1/satellite/tle/25544&apiKey=REDACTED")]
    [InlineData("https://api.n2yo.com/rest/v1/satellite/positions/25544/32/34/0/300/&apiKey=abc123",
                "https://api.n2yo.com/rest/v1/satellite/positions/25544/32/34/0/300/&apiKey=REDACTED")]
    [InlineData("https://example.com/x?APIKEY=abc&other=1",
                "https://example.com/x?APIKEY=REDACTED&other=1")]
    [InlineData("https://example.com/x?other=1",
                "https://example.com/x?other=1")]
    public void Redact_ReplacesApiKeyValue(string url, string expected)
    {
        Assert.Equal(expected, N2YOHttpClientLogger.Redact(new Uri(url)));
    }

    [Fact]
    public async Task N2YOClient_LogsRequests_WithoutApiKey()
    {
        var (client, logs) = BuildClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") }));

        await client.GetTleAsync(25544);

        Assert.NotEmpty(logs);
        Assert.DoesNotContain(logs, l => l.Contains(SecretKey));
        Assert.Contains(logs, l => l.Contains("apiKey=REDACTED"));
    }

    [Fact]
    public async Task N2YOClient_LogsFailedRequests_WithoutApiKey()
    {
        var (client, logs) = BuildClient(new StubHandler(req =>
            throw new HttpRequestException($"connection refused for {req.RequestUri}")));

        var result = await client.GetTleAsync(25544);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(logs);
        Assert.DoesNotContain(logs, l => l.Contains(SecretKey));
    }

    // Same check through the real host: Program.cs registration, the validated N2YoOptions and the
    // logging levels from appsettings.json (System.Net.Http.HttpClient at Warning), with only the
    // primary HTTP handler faked so nothing reaches N2YO.
    [Fact]
    public async Task N2YOClient_InRealHost_LogsNothingContainingApiKey()
    {
        var logs = new ConcurrentQueue<string>();
        using var factory = new CustomWebApplicationFactory(new Dictionary<string, string?> { ["N2YO:ApiKey"] = SecretKey })
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(l => l.AddProvider(new CapturingLoggerProvider(logs)));
                builder.ConfigureTestServices(services =>
                    services.AddHttpClient<IN2YOClient, N2YOClient>().ConfigurePrimaryHttpMessageHandler(() =>
                        new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") })));
            });

        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IN2YOClient>().GetTleAsync(25544);

        Assert.Contains(logs, l => l.Contains("apiKey=REDACTED"));
        Assert.DoesNotContain(logs, l => l.Contains(SecretKey));
    }

    // Builds the client through the same registration Program.cs uses, with a capturing logger
    // at Trace so any default HttpClient logger that slipped back in would be caught.
    private static (IN2YOClient Client, ConcurrentQueue<string> Logs) BuildClient(HttpMessageHandler handler)
    {
        var logs = new ConcurrentQueue<string>();
        var services = new ServiceCollection();
        services.AddOptions<N2YoOptions>().Configure(o => o.ApiKey = SecretKey);
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(new CapturingLoggerProvider(logs)));
        services.AddN2YOClient().ConfigurePrimaryHttpMessageHandler(() => handler);

        return (services.BuildServiceProvider().GetRequiredService<IN2YOClient>(), logs);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(respond(request));
    }

    private sealed class CapturingLoggerProvider(ConcurrentQueue<string> logs) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, logs);
        public void Dispose() { }
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<string> logs) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            logs.Enqueue($"{category} scope: {state}");
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => logs.Enqueue($"{category}: {formatter(state, exception)} {exception}");
    }
}
