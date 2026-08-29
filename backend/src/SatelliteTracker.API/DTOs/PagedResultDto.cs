namespace SatelliteTracker.API.DTOs;

// Deliberate exception to the "bare array" convention every other list endpoint in this API
// follows — pagination requires metadata (page/pageSize/hasMore) a bare array can't carry.
// See CLAUDE.md's Pass History section.
public record PagedResultDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, bool HasMore);
