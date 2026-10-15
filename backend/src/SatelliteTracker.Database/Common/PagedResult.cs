namespace SatelliteTracker.Database.Common;

// Pagination envelope for repository/service methods that page results. HasMore is determined by
// the caller querying PageSize+1 rows and checking whether the extra row exists, rather than a
// separate COUNT query.
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, bool HasMore);
