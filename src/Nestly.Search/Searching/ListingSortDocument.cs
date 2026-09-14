using Nestly.Domain;

namespace Nestly.Search.Searching;

/// <summary>The fields a non-relevance sort needs, fetched on the retrieval legs.</summary>
// Deserializing into Listing would fail: its properties are required, and the legs fetch at most
// one field.
internal sealed record ListingSortDocument
{
    public int MonthlyRent { get; init; }

    public double? ReviewScore { get; init; }

    public GeoPoint? Location { get; init; }
}
