namespace Nestly.Domain;

/// <summary>
/// Structured filters. All of these are pushed into both retrieval legs -- the lexical query and
/// the kNN query -- so vector hits never escape the user's constraints.
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

    /// <summary>Radius search centre. Filters only when <see cref="RadiusKm"/> is supplied; on its own it just measures distance to each hit.</summary>
    public GeoPoint? Near { get; init; }

    public double? RadiusKm { get; init; }

    /// <summary>Map viewport constraint, applied on top of any radius filter.</summary>
    public GeoBounds? Within { get; init; }
}
