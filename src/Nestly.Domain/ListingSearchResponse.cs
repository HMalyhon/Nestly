namespace Nestly.Domain;

public sealed record ListingSearchResponse
{
    public required long Total { get; init; }

    public required IReadOnlyList<ListingHit> Hits { get; init; }

    public required ListingFacets Facets { get; init; }

    /// <summary>Server-side timing, surfaced in the UI to make the speed claim checkable.</summary>
    public required long ElapsedMs { get; init; }
}
