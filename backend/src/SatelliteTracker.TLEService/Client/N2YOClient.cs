using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SatelliteTracker.Database.Common;
using SatelliteTracker.TLEService.Client.DTOs;

namespace SatelliteTracker.TLEService.Client;

//This class implements the IN2YOClient interface to interact with the N2YO API for satellite tracking data.
public class N2YOClient : IN2YOClient
{
    private readonly HttpClient _httpClient; // HttpClient instance for making API requests.
    private readonly string _apiKey; // API key for authenticating with the N2YO API.

    // Base URL for the N2YO API endpoints.
    private const string BaseUrl = "https://api.n2yo.com/rest/v1/satellite/";

    // JSON serializer options to ensure case-insensitive property matching when deserializing API responses.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Constructor that initializes the HttpClient and retrieves the API key from the configuration.
    public N2YOClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["N2YO:ApiKey"] ?? throw new InvalidOperationException("N2YO:ApiKey is not configured."); // Retrieve the API key from configuration, throwing an exception if it's not set.
    }

    // This method retrieves the Two-Line Element (TLE) data for a satellite given its NORAD ID.
    public async Task<Result<TleResponse>> GetTleAsync(int noradId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}tle/{noradId}&apiKey={_apiKey}";
        return await GetAsync<TleResponse>(url, ct);
    }

    // This method retrieves the current position of a satellite based on its NORAD ID and the observer's location (latitude, longitude, altitude) and the time window in seconds.
    public async Task<Result<PositionResponse>> GetPositionsAsync(int noradId, double lat, double lng, double alt, int seconds, CancellationToken ct = default)
    {
        // Construct the URL for the positions endpoint, including all required parameters and the API key.
        var url = string.Format(CultureInfo.InvariantCulture,"{0}positions/{1}/{2}/{3}/{4}/{5}/&apiKey={6}",BaseUrl, noradId, lat, lng, alt, seconds, _apiKey);

        return await GetAsync<PositionResponse>(url, ct);
    }

    // This method retrieves the upcoming radio passes for a satellite based on its NORAD ID and the observer's location (latitude, longitude, altitude),
    // the number of days to look ahead, and the minimum elevation angle for the passes.
    public async Task<Result<RadioPassResponse>> GetRadioPassesAsync( int noradId, double lat, double lng, double alt, int days, int minElevation,CancellationToken ct = default)
    {
        var url = string.Format(CultureInfo.InvariantCulture,
            "{0}radiopasses/{1}/{2}/{3}/{4}/{5}/{6}/&apiKey={7}",
            BaseUrl, noradId, lat, lng, alt, days, minElevation, _apiKey);

        return await GetAsync<RadioPassResponse>(url, ct);
    }
    // This private helper method performs the actual HTTP GET request to the specified URL and handles the response.
    private async Task<Result<T>> GetAsync<T>(string url, CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode) // If the response status code indicates an error, return a failure result with the status code and reason phrase.
                return Result<T>.Failure($"N2YO API returned {(int)response.StatusCode}: {response.ReasonPhrase}");

            var content = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<T>(content, JsonOptions);

            return result is null? Result<T>.Failure("Failed to parse N2YO API response."): Result<T>.Success(result);
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
