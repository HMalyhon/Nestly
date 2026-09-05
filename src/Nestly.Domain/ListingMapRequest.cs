namespace Nestly.Domain;

/// <summary>What the map asks for: the same search, minus paging and sorting.</summary>
public sealed record ListingMapRequest
{
    public string? Query { get; init; }

    public ListingFilters Filters { get; init; } = new();

    /// <summary>Leaflet zoom level, used to size the grid cells when results are clustered.</summary>
    public int Zoom { get; init; } = 12;
}
