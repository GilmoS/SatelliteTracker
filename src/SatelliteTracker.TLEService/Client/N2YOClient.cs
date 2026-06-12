using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SatelliteTracker.Database.Common;
using SatelliteTracker.TLEService.Client.DTOs;

namespace SatelliteTracker.TLEService.Client;

public class N2YOClient : IN2YOClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    private const string BaseUrl = "https://api.n2yo.com/rest/v1/satellite/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public N2YOClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["N2YO:ApiKey"]
            ?? throw new InvalidOperationException("N2YO:ApiKey is not configured.");
    }

    public async Task<Result<TleResponse>> GetTleAsync(int noradId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}tle/{noradId}&apiKey={_apiKey}";
        return await GetAsync<TleResponse>(url, ct);
    }

    public async Task<Result<PositionResponse>> GetPositionsAsync(
        int noradId, double lat, double lng, double alt, int seconds,
        CancellationToken ct = default)
    {
        var url = string.Format(CultureInfo.InvariantCulture,
            "{0}positions/{1}/{2}/{3}/{4}/{5}/&apiKey={6}",
            BaseUrl, noradId, lat, lng, alt, seconds, _apiKey);

        return await GetAsync<PositionResponse>(url, ct);
    }

    public async Task<Result<RadioPassResponse>> GetRadioPassesAsync(
        int noradId, double lat, double lng, double alt, int days, int minElevation,
        CancellationToken ct = default)
    {
        var url = string.Format(CultureInfo.InvariantCulture,
            "{0}radiopasses/{1}/{2}/{3}/{4}/{5}/{6}/&apiKey={7}",
            BaseUrl, noradId, lat, lng, alt, days, minElevation, _apiKey);

        return await GetAsync<RadioPassResponse>(url, ct);
    }

    private async Task<Result<T>> GetAsync<T>(string url, CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
                return Result<T>.Failure(
                    $"N2YO API returned {(int)response.StatusCode}: {response.ReasonPhrase}");

            var content = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<T>(content, JsonOptions);

            return result is null
                ? Result<T>.Failure("Failed to parse N2YO API response.")
                : Result<T>.Success(result);
        }
        catch (JsonException ex)
        {
            return Result<T>.Failure($"JSON parse error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<T>.Failure($"HTTP error: {ex.Message}");
        }
    }
}
