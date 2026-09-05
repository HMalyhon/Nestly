using Nestly.Domain;

namespace Nestly.Search.Searching;

/// <summary>The three fields a map marker needs, matching the trimmed _source the map asks for.</summary>
// Deserializing into Listing would fail: its properties are required, and the map deliberately
// fetches only a fraction of them.
internal sealed record ListingMapDocument
{
    public string Id { get; init; } = string.Empty;

    public GeoPoint Location { get; init; }

    public int MonthlyRent { get; init; }
}
