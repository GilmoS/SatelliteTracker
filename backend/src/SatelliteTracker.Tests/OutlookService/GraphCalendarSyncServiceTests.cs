using SatelliteTracker.OutlookService.Abstractions;
using SatelliteTracker.OutlookService.Services;
using Xunit;

namespace SatelliteTracker.Tests.OutlookService;

public class GraphCalendarSyncServiceTests
{
    private readonly GraphCalendarSyncService _service = new();

    [Fact]
    public async Task SyncPassesAsync_ThrowsNotImplementedException()
    {
        await Assert.ThrowsAsync<NotImplementedException>(() =>
            _service.SyncPassesAsync([], new CalendarSyncSettings([], null)));
    }

    [Fact]
    public async Task CancelSyncedPassesAsync_ThrowsNotImplementedException()
    {
        await Assert.ThrowsAsync<NotImplementedException>(() =>
            _service.CancelSyncedPassesAsync([]));
    }
}
