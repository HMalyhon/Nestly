using CsvHelper.Configuration.Attributes;

namespace Nestly.Seeder.Csv;

/// <summary>
/// One row of the dataset, exactly as published: every column a string, nothing parsed.
/// </summary>
/// <remarks>
/// Keeping the file's shape separate from the domain model means the mapping is one testable
/// step -- and that a change to the upstream column names shows up as a compile error in one
/// file rather than as silently empty fields spread across the seeder.
/// </remarks>
internal sealed record ListingCsvRow
{
    [Name("id")]
    public string Id { get; init; } = string.Empty;

    [Name("name")]
    public string Name { get; init; } = string.Empty;

    [Name("description")]
    public string Description { get; init; } = string.Empty;

    [Name("neighbourhood_cleansed")]
    public string Neighborhood { get; init; } = string.Empty;

    [Name("neighbourhood_group_cleansed")]
    public string Borough { get; init; } = string.Empty;

    [Name("latitude")]
    public string Latitude { get; init; } = string.Empty;

    [Name("longitude")]
    public string Longitude { get; init; } = string.Empty;

    [Name("property_type")]
    public string PropertyType { get; init; } = string.Empty;

    [Name("room_type")]
    public string RoomType { get; init; } = string.Empty;

    [Name("accommodates")]
    public string Accommodates { get; init; } = string.Empty;

    [Name("bathrooms_text")]
    public string BathroomsText { get; init; } = string.Empty;

    [Name("bedrooms")]
    public string Bedrooms { get; init; } = string.Empty;

    [Name("amenities")]
    public string Amenities { get; init; } = string.Empty;

    [Name("price")]
    public string Price { get; init; } = string.Empty;

    [Name("minimum_nights")]
    public string MinimumNights { get; init; } = string.Empty;

    [Name("review_scores_rating")]
    public string ReviewScore { get; init; } = string.Empty;

    [Name("last_review")]
    public string LastReview { get; init; } = string.Empty;
}
