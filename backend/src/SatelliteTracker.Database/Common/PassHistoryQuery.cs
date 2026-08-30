namespace SatelliteTracker.Database.Common;

// Optional per-field filters and pagination for IPassRepository.GetHistoryAsync. Every filter
// is independently optional and AND-combined when multiple are present; a null bound means
// "no filter on that field." Sort order is fixed (Aos descending) — not part of this query.
public record PassHistoryQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public int? OrbitNumberFrom { get; init; }
    public int? OrbitNumberTo { get; init; }
    public decimal? MaxElevationFrom { get; init; }
    public decimal? MaxElevationTo { get; init; }
    public DateTime? AosFrom { get; init; }
    public DateTime? AosTo { get; init; }
    public DateTime? LosFrom { get; init; }
    public DateTime? LosTo { get; init; }
}
