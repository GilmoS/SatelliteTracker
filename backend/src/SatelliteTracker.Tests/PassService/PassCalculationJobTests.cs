using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using SatelliteTracker.Database.Common;
using SatelliteTracker.Database.Entities;
using SatelliteTracker.Database.Repositories;
using SatelliteTracker.PassService.SGP4;
using SatelliteTracker.PassService.Services;
using Xunit;

namespace SatelliteTracker.Tests.PassService;

public class PassCalculationJobTests
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

    private readonly Mock<ISatelliteRepository> _satelliteRepo = new();
    private readonly Mock<IPassService> _passService = new();
    private readonly CapturingLogger _logger = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero));

    // Released once per GetActiveAsync call, i.e. once per job run.
    private readonly SemaphoreSlim _runStarted = new(0);

    private static readonly Satellite ErosC3 = new() { Id = Guid.NewGuid(), Name = "EROS C3", NoradId = 54880, IsActive = true };
    private static readonly Satellite Runner1 = new() { Id = Guid.NewGuid(), Name = "RUNNER 1", NoradId = 62673, IsActive = true };

    private PassCalculationJob CreateJob()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_satelliteRepo.Object);
        services.AddSingleton(_passService.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return new PassCalculationJob(_logger, scopeFactory, _time);
    }

    private void SetupActiveSatellites(params Satellite[] satellites) =>
        _satelliteRepo.Setup(r => r.GetActiveAsync())
            .ReturnsAsync(() =>
            {
                _runStarted.Release();
                return Result<IEnumerable<Satellite>>.Success(satellites);
            });

    private void SetupCalculationSucceeds() =>
        _passService.Setup(s => s.CalculateAndSavePassesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<PassResult>>.Success([]));

    private async Task WaitForRunAsync() =>
        Assert.True(await _runStarted.WaitAsync(WaitTimeout), "Expected the job to start a run");

    // ---- Per-run behavior ----

    [Fact]
    public async Task RunOnceAsync_CalculatesPassesForEveryActiveSatellite()
    {
        SetupActiveSatellites(ErosC3, Runner1);
        SetupCalculationSucceeds();

        await CreateJob().RunOnceAsync(CancellationToken.None);

        _passService.Verify(s => s.CalculateAndSavePassesAsync(ErosC3.Id, It.IsAny<CancellationToken>()), Times.Once);
        _passService.Verify(s => s.CalculateAndSavePassesAsync(Runner1.Id, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(_logger.Errors);
    }

    [Fact]
    public async Task RunOnceAsync_OneSatelliteFails_LogsErrorAndContinuesWithTheNext()
    {
        SetupActiveSatellites(ErosC3, Runner1);
        _passService.Setup(s => s.CalculateAndSavePassesAsync(ErosC3.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<PassResult>>.Failure("No TLE"));
        _passService.Setup(s => s.CalculateAndSavePassesAsync(Runner1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<PassResult>>.Success([]));

        await CreateJob().RunOnceAsync(CancellationToken.None);

        _passService.Verify(s => s.CalculateAndSavePassesAsync(Runner1.Id, It.IsAny<CancellationToken>()), Times.Once);
        var error = Assert.Single(_logger.Errors);
        Assert.Contains("EROS C3", error.Message);
    }

    [Fact]
    public async Task RunOnceAsync_ActiveSatellitesLookupFails_LogsErrorAndCalculatesNothing()
    {
        _satelliteRepo.Setup(r => r.GetActiveAsync())
            .ReturnsAsync(Result<IEnumerable<Satellite>>.Failure("db down"));

        await CreateJob().RunOnceAsync(CancellationToken.None);

        _passService.Verify(s => s.CalculateAndSavePassesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Single(_logger.Errors);
    }

    // ---- Schedule ----

    [Fact]
    public async Task Job_RunsAtStartupThenHourly()
    {
        SetupActiveSatellites(ErosC3);
        SetupCalculationSucceeds();
        var job = CreateJob();

        await job.StartAsync(CancellationToken.None);
        await WaitForRunAsync();

        _time.Advance(PassCalculationJob.Interval - TimeSpan.FromMinutes(1));
        Assert.False(await _runStarted.WaitAsync(TimeSpan.FromMilliseconds(200)), "Ran before the hour was up");

        _time.Advance(TimeSpan.FromMinutes(1));
        await WaitForRunAsync();

        await job.StopAsync(CancellationToken.None);
        _passService.Verify(s => s.CalculateAndSavePassesAsync(ErosC3.Id, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ---- Failure isolation ----

    [Fact]
    public async Task Job_ExceptionInARun_IsLoggedAsErrorAndTheNextRunStillHappens()
    {
        var boom = new InvalidOperationException("boom");
        var calls = 0;
        _satelliteRepo.Setup(r => r.GetActiveAsync())
            .Returns(() =>
            {
                _runStarted.Release();
                return Interlocked.Increment(ref calls) == 1
                    ? Task.FromException<Result<IEnumerable<Satellite>>>(boom)
                    : Task.FromResult(Result<IEnumerable<Satellite>>.Success(new[] { ErosC3 }));
            });
        SetupCalculationSucceeds();
        var job = CreateJob();

        await job.StartAsync(CancellationToken.None);
        await WaitForRunAsync();

        _time.Advance(PassCalculationJob.Interval);
        await WaitForRunAsync();
        await job.StopAsync(CancellationToken.None);

        var error = Assert.Single(_logger.Errors);
        Assert.Same(boom, error.Exception);
        _passService.Verify(s => s.CalculateAndSavePassesAsync(ErosC3.Id, It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(job.ExecuteTask!.IsCompletedSuccessfully);
    }

    // ---- Cancellation ----

    [Fact]
    public async Task Job_StoppedWhileWaitingForNextTick_StopsCleanlyWithoutErrorLog()
    {
        SetupActiveSatellites(ErosC3);
        SetupCalculationSucceeds();
        var job = CreateJob();

        await job.StartAsync(CancellationToken.None);
        await WaitForRunAsync();
        await job.StopAsync(CancellationToken.None);

        Assert.True(job.ExecuteTask!.IsCompletedSuccessfully);
        Assert.Empty(_logger.Errors);
    }

    [Fact]
    public async Task Job_StoppedMidRun_StopsCleanlyWithoutErrorLog()
    {
        SetupActiveSatellites(ErosC3, Runner1);
        var calculationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _passService.Setup(s => s.CalculateAndSavePassesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid _, CancellationToken ct) =>
            {
                calculationStarted.TrySetResult();
                await Task.Delay(Timeout.Infinite, ct); // only ends when the job is stopped
                return Result<IEnumerable<PassResult>>.Success([]);
            });
        var job = CreateJob();

        await job.StartAsync(CancellationToken.None);
        await calculationStarted.Task.WaitAsync(WaitTimeout);
        await job.StopAsync(CancellationToken.None);

        Assert.True(job.ExecuteTask!.IsCompletedSuccessfully);
        Assert.Empty(_logger.Errors);
        // Stopped during EROS C3, so RUNNER 1 was never started.
        _passService.Verify(s => s.CalculateAndSavePassesAsync(Runner1.Id, It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class CapturingLogger : ILogger<PassCalculationJob>
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();

        public IReadOnlyList<LogEntry> Errors =>
            _entries.Where(e => e.Level >= LogLevel.Error).ToList();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            _entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception));
    }
}
