namespace Nestly.Domain;

/// <summary>A result row: the listing plus why it matched.</summary>
public sealed record ListingHit
{
    public required Listing Listing { get; init; }

    /// <summary>Relevance score, zero when the results were sorted by something else. Becomes the fused RRF score once hybrid search lands.</summary>
    public required double Score { get; init; }

    /// <summary>Highlighted description snippets, with <c>&lt;em&gt;</c> around matches.</summary>
    public IReadOnlyList<string> Highlights { get; init; } = [];

    /// <summary>Which retrieval leg(s) surfaced this listing. Drives the "why" badge in the UI.</summary>
    public required MatchSource MatchedBy { get; init; }

    /// <summary>Kilometres from <see cref="ListingFilters.Near"/>, when a radius search ran.</summary>
    public double? DistanceKm { get; init; }
}
