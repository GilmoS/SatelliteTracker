using System.Text.RegularExpressions;
using Microsoft.Extensions.Http.Logging;

namespace SatelliteTracker.API.Http;

// Replaces IHttpClientFactory's default request logging for the N2YO client.
// N2YO takes the API key as an "apiKey" URL parameter, and the default loggers write the full
// request URL at Information level, so every N2YO call used to put the key in the logs.
// This logger writes the same start/stop/failure lines with the key value replaced.
public partial class N2YOHttpClientLogger : IHttpClientLogger
{
    private const string Redacted = "REDACTED";

    private readonly ILogger<N2YOHttpClientLogger> _logger;

    public N2YOHttpClientLogger(ILogger<N2YOHttpClientLogger> logger)
    {
        _logger = logger;
    }

    // Replaces the value of every apiKey parameter (any case) in the URL.
    public static string Redact(Uri? uri)
    {
        if (uri is null)
            return string.Empty;

        return ApiKeyPattern().Replace(uri.OriginalString, $"${{name}}{Redacted}");
    }

    public object? LogRequestStart(HttpRequestMessage request)
    {
        _logger.LogInformation("Sending HTTP request {Method} {Uri}", request.Method, Redact(request.RequestUri));
        return null;
    }

    public void LogRequestStop(object? context, HttpRequestMessage request, HttpResponseMessage response, TimeSpan elapsed)
    {
        _logger.LogInformation("Received HTTP response {StatusCode} for {Method} {Uri} after {ElapsedMs}ms",
            (int)response.StatusCode, request.Method, Redact(request.RequestUri), elapsed.TotalMilliseconds);
    }

    public void LogRequestFailed(object? context, HttpRequestMessage request, HttpResponseMessage? response, Exception exception, TimeSpan elapsed)
    {
        // The exception is logged by type and message only, since a message could in theory echo the URL.
        _logger.LogWarning("HTTP request {Method} {Uri} failed after {ElapsedMs}ms: {ExceptionType}: {ExceptionMessage}",
            request.Method, Redact(request.RequestUri), elapsed.TotalMilliseconds,
            exception.GetType().Name, Redact(exception.Message));
    }

    private static string Redact(string text) => ApiKeyPattern().Replace(text, $"${{name}}{Redacted}");

    [GeneratedRegex(@"(?<name>apiKey=)[^&/\s]*", RegexOptions.IgnoreCase)]
    private static partial Regex ApiKeyPattern();
}
