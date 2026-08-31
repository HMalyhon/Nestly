namespace Nestly.Domain;

/// <summary>
/// Facet counts. Each list is computed with every active filter applied *except* its own
/// dimension, so selecting a borough does not collapse the borough counts to a single row.
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
