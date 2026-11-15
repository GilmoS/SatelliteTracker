using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SatelliteTracker.Tests.API.Infrastructure;

public sealed record CapturedLogEntry(string Category, LogLevel Level, string Message);

// Keeps every log entry (formatted message plus any exception text) in memory, for tests that
// assert on what the host logged.
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLogEntry> _entries = new();

    public IReadOnlyList<CapturedLogEntry> Entries => _entries.ToList();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

    public void Dispose() { }

    private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => entries.Enqueue(new CapturedLogEntry(category, logLevel, formatter(state, exception) + exception));
    }
}
