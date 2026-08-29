namespace Nestly.Domain;

/// <summary>Everything the UI can ask for in one search.</summary>
public sealed record ListingSearchRequest
{
    /// <summary>Free text. Empty or null runs a filters-only browse.</summary>
    public string? Query { get; init; }

    public ListingFilters Filters { get; init; } = new();

    public ListingSort Sort { get; init; } = ListingSort.Relevance;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}

/// <summary>
/// Structured filters. All of these are pushed into both retrieval legs — the lexical query and
/// the kNN query — so vector hits never escape the user's constraints.
/// </summary>
public sealed record ListingFilters
{
    public int? MinRent { get; init; }

    public int? MaxRent { get; init; }

    /// <summary>Bedroom counts to include; 0 means studio. Empty means no constraint.</summary>
    public IReadOnlyList<byte> Bedrooms { get; init; } = [];

    public decimal? MinBathrooms { get; init; }

    public byte? MinAccommodates { get; init; }

    public IReadOnlyList<string> Boroughs { get; init; } = [];

    public IReadOnlyList<string> Neighborhoods { get; init; } = [];

    public IReadOnlyList<string> RoomTypes { get; init; } = [];

    public IReadOnlyList<string> PropertyTypes { get; init; } = [];

    /// <summary>Amenities are conjunctive: a listing must have all of them, not any.</summary>
    public IReadOnlyList<string> Amenities { get; init; } = [];

    public double? MinReviewScore { get; init; }

    /// <summary>Radius search. Ignored unless both centre and radius are supplied.</summary>
    public GeoPoint? Near { get; init; }

    public double? RadiusKm { get; init; }

    /// <summary>Map viewport constraint, applied on top of any radius filter.</summary>
    public GeoBounds? Within { get; init; }
}

/// <summary>A map viewport, as an Elasticsearch <c>geo_bounding_box</c>.</summary>
public readonly record struct GeoBounds(double TopLat, double LeftLon, double BottomLat, double RightLon);

public enum ListingSort
{
    Relevance,
    PriceAsc,
    PriceDesc,
    ReviewScoreDesc,
    DistanceAsc,
}

public sealed record ListingSearchResponse
{
    public required long Total { get; init; }

    public required IReadOnlyList<ListingHit> Hits { get; init; }

    public required ListingFacets Facets { get; init; }

    /// <summary>Server-side timing, surfaced in the UI to make the speed claim checkable.</summary>
    public required long ElapsedMs { get; init; }
}

/// <summary>A result row: the listing plus why it matched.</summary>
public sealed record ListingHit
{
    public required Listing Listing { get; init; }

    /// <summary>Fused RRF score, not a raw Elasticsearch <c>_score</c>.</summary>
    public required double Score { get; init; }

    /// <summary>Highlighted description snippets, with <c>&lt;em&gt;</c> around matches.</summary>
    public IReadOnlyList<string> Highlights { get; init; } = [];

    /// <summary>Which retrieval leg(s) surfaced this listing. Drives the "why" badge in the UI.</summary>
    public required MatchSource MatchedBy { get; init; }

    /// <summary>Kilometres from <see cref="ListingFilters.Near"/>, when a radius search ran.</summary>
    public double? DistanceKm { get; init; }
}

[Flags]
public enum MatchSource
{
    None = 0,
    Lexical = 1,
    Vector = 2,
    Both = Lexical | Vector,
}

/// <summary>
/// Facet counts. Each list is computed with every active filter applied *except* its own
/// dimension, so selecting a borough does not collapse the borough counts to one row.
/// </summary>
public sealed record ListingFacets
{
    public IReadOnlyList<FacetBucket> Boroughs { get; init; } = [];

    public IReadOnlyList<FacetBucket> Neighborhoods { get; init; } = [];

    public IReadOnlyList<FacetBucket> Bedrooms { get; init; } = [];

    public IReadOnlyList<FacetBucket> RoomTypes { get; init; } = [];

    public IReadOnlyList<FacetBucket> PropertyTypes { get; init; } = [];

    public IReadOnlyList<FacetBucket> Amenities { get; init; } = [];

    /// <summary>Rent histogram, for the price slider's background distribution.</summary>
    public IReadOnlyList<FacetBucket> RentHistogram { get; init; } = [];

    public int? MinRent { get; init; }

    public int? MaxRent { get; init; }
}

public readonly record struct FacetBucket(string Key, long Count);
