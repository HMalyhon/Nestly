namespace Nestly.Domain;

/// <summary>
/// A single apartment listing as stored in Elasticsearch.
/// </summary>
/// <remarks>
/// Every field except <see cref="MonthlyRent"/> comes straight from the Inside Airbnb NYC
/// dataset. See data/README.md for provenance and the one derived value.
/// </remarks>
public sealed record Listing
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public required string Neighborhood { get; init; }

    /// <summary>One of the five NYC boroughs.</summary>
    public required string Borough { get; init; }

    public required GeoPoint Location { get; init; }

    /// <summary>Nightly price as published by the source dataset, in whole dollars.</summary>
    public required int PricePerNight { get; init; }

    /// <summary>
    /// Derived from <see cref="PricePerNight"/>. The source is short-term rental data, so there
    /// is no real monthly rent to read; this is the only fabricated value in the index and the
    /// README says so plainly.
    /// </summary>
    public required int MonthlyRent { get; init; }

    /// <summary>Zero means a studio.</summary>
    public required byte Bedrooms { get; init; }

    public required decimal Bathrooms { get; init; }

    public required byte Accommodates { get; init; }

    public required string PropertyType { get; init; }

    public required string RoomType { get; init; }

    public required IReadOnlyList<string> Amenities { get; init; }

    /// <summary>Source rating out of 5. Null when the listing has no reviews yet.</summary>
    public double? ReviewScore { get; init; }

    public required short MinimumNights { get; init; }

    public DateOnly? LastReviewedAt { get; init; }

    /// <summary>
    /// 384-dimension embedding of <see cref="Description"/>, produced in-process by
    /// all-MiniLM-L6-v2. Null until the seeder's embedding pass has run.
    /// </summary>
    public float[]? DescriptionVector { get; init; }
}

/// <summary>A WGS84 coordinate pair, mapped to an Elasticsearch <c>geo_point</c>.</summary>
public readonly record struct GeoPoint(double Lat, double Lon);
