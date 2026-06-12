using System.Net;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using SatelliteTracker.TLEService.Client;
using Xunit;

namespace SatelliteTracker.Tests.TLEService;

public class N2YOClientTests
{
    private static IN2YOClient CreateClient(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseJson)
            });

        var httpClient = new HttpClient(handler.Object);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["N2YO:ApiKey"] = "test-key" })
            .Build();

        return new N2YOClient(httpClient, config);
    }

    // ── GetTleAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTleAsync_Success_ReturnsTleResponse()
    {
        const string json = """
            {
              "info": { "satid": 25544, "satname": "ISS", "transactionscount": 1 },
              "tle": "1 25544U 98067A   23001.00000000  .00000000  00000-0  00000-0 0  9999\r\n2 25544  51.6400 208.9163 0001382  95.6617 344.7248 15.49309522223145"
            }
            """;

        var client = CreateClient(json);
        var result = await client.GetTleAsync(25544);

        Assert.True(result.IsSuccess);
        Assert.Equal(25544, result.Value!.Info.SatId);
        Assert.Equal("ISS", result.Value.Info.SatName);
        Assert.Contains("25544", result.Value.Tle);
    }

    [Fact]
    public async Task GetTleAsync_HttpError_ReturnsFailure()
    {
        var client = CreateClient(string.Empty, HttpStatusCode.Unauthorized);
        var result = await client.GetTleAsync(25544);

        Assert.False(result.IsSuccess);
        Assert.Contains("401", result.Error);
    }

    [Fact]
    public async Task GetTleAsync_MalformedJson_ReturnsFailure()
    {
        var client = CreateClient("not valid json }{");
        var result = await client.GetTleAsync(25544);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    // ── GetPositionsAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetPositionsAsync_Success_ReturnsPositionResponse()
    {
        const string json = """
            {
              "info": { "satname": "ISS", "satid": 25544, "transactionscount": 1 },
              "positions": [
                { "satlatitude": 32.0, "satlongitude": 34.8, "sataltitude": 408.5,
                  "azimuth": 180.5, "elevation": 45.2, "ra": 12.3, "dec": 45.0, "timestamp": 1672531200 }
              ]
            }
            """;

        var client = CreateClient(json);
        var result = await client.GetPositionsAsync(25544, 32.0055, 34.8854, 135, 1);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Positions);
        Assert.Equal(32.0, result.Value.Positions[0].SatLatitude);
    }

    [Fact]
    public async Task GetPositionsAsync_HttpError_ReturnsFailure()
    {
        var client = CreateClient(string.Empty, HttpStatusCode.ServiceUnavailable);
        var result = await client.GetPositionsAsync(25544, 32.0, 34.8, 135, 1);

        Assert.False(result.IsSuccess);
        Assert.Contains("503", result.Error);
    }

    [Fact]
    public async Task GetPositionsAsync_MalformedJson_ReturnsFailure()
    {
        var client = CreateClient("{positions: oops}");
        var result = await client.GetPositionsAsync(25544, 32.0, 34.8, 135, 1);

        Assert.False(result.IsSuccess);
    }

    // ── GetRadioPassesAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetRadioPassesAsync_Success_ReturnsRadioPassResponse()
    {
        const string json = """
            {
              "info": { "satid": 25544, "satname": "ISS", "transactionscount": 1, "passescount": 1 },
              "passes": [
                { "startAz": 45.2, "startAzCompass": "NE", "startUTC": 1672531200,
                  "maxAz": 180.5, "maxAzCompass": "S", "maxEl": 75.2, "maxUTC": 1672531300,
                  "endAz": 315.0, "endAzCompass": "NW", "endUTC": 1672531400, "duration": 200 }
              ]
            }
            """;

        var client = CreateClient(json);
        var result = await client.GetRadioPassesAsync(25544, 32.0055, 34.8854, 135, 7, 10);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Passes);
        Assert.Equal(1, result.Value.Info.PassesCount);
        Assert.Equal(75.2, result.Value.Passes[0].MaxEl);
    }

    [Fact]
    public async Task GetRadioPassesAsync_HttpError_ReturnsFailure()
    {
        var client = CreateClient(string.Empty, HttpStatusCode.TooManyRequests);
        var result = await client.GetRadioPassesAsync(25544, 32.0, 34.8, 135, 7, 10);

        Assert.False(result.IsSuccess);
        Assert.Contains("429", result.Error);
    }

    [Fact]
    public async Task GetRadioPassesAsync_MalformedJson_ReturnsFailure()
    {
        var client = CreateClient("<<<invalid>>>");
        var result = await client.GetRadioPassesAsync(25544, 32.0, 34.8, 135, 7, 10);

        Assert.False(result.IsSuccess);
    }
}
