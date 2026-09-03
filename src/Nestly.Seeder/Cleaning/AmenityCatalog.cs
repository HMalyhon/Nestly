using System.Text.Json;

namespace Nestly.Seeder.Cleaning;

/// <summary>
/// Collapses Airbnb's free-form amenity strings onto a small canonical vocabulary.
/// </summary>
/// <remarks>
/// 5,000 listings carry 2,085 distinct amenity strings: "Wifi", "Fast wifi -- 109 Mbps",
/// "52 inch HDTV with Fire TV", "Summit stainless steel oven". Faceting on the raw values would
/// produce a filter list longer than the result list and a count of 1 beside most of it, so the
/// long tail is folded into the terms a renter would actually filter by. Anything that does not
/// map is dropped rather than indexed: an amenity nobody can filter on is dead weight in every
/// document.
/// </remarks>
internal static class AmenityCatalog
{
    /// <summary>
    /// Substring rules rather than exact matches, because the tail is mostly the canonical term
    /// with a brand or a size bolted on. The exclusions are the traps: "dishwasher" contains
    /// "washer", "hair dryer" contains "dryer", and a "Whirlpool refrigerator" is not a pool.
    /// </summary>
    private static readonly AmenityRule[] Rules =
    [
        new("Wifi", ["wifi"], []),
        new("Air conditioning", ["air conditioning", "central air"], []),
        new("Heating", ["heating"], []),
        new("Kitchen", ["kitchen"], []),
        new("Dishwasher", ["dishwasher"], []),
        new("Washer", ["washer"], ["dishwasher"]),
        new("Dryer", ["dryer"], ["hair dryer"]),
        new("Elevator", ["elevator"], []),
        new("Gym", ["gym", "exercise equipment"], []),
        new("Pool", ["pool"], ["whirlpool", "pool table", "liverpool"]),
        new("Hot tub", ["hot tub"], []),
        new("Free parking", ["free parking", "free street parking", "free driveway parking", "free carport"], []),
        new("Paid parking", ["paid parking", "paid street parking"], []),
        new("Pets allowed", ["pets allowed"], []),
        new("Outdoor space", ["patio or balcony", "backyard", "terrace"], []),
        new("TV", ["tv"], []),
        new("Dedicated workspace", ["dedicated workspace"], []),
        new("Self check-in", ["self check-in"], []),
        new("Private entrance", ["private entrance"], []),
        new("Long term stays allowed", ["long term stays allowed"], []),
    ];

    /// <summary>
    /// Maps one listing's <c>amenities</c> column -- a JSON array -- onto the canonical set.
    /// Returns an empty list for a malformed or absent value rather than failing the row: an
    /// unparseable amenity list is not a reason to drop a real apartment.
    /// </summary>
    public static IReadOnlyList<string> Normalize(string? amenitiesJson)
    {
        if (string.IsNullOrWhiteSpace(amenitiesJson))
        {
            return [];
        }

        string[]? amenities;

        try
        {
            amenities = JsonSerializer.Deserialize<string[]>(amenitiesJson);
        }
        catch (JsonException)
        {
            return [];
        }

        if (amenities is null)
        {
            return [];
        }

        var canonical = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var amenity in amenities)
        {
            // The source is littered with non-breaking spaces ("Washer - In unit"), which
            // would otherwise defeat the substring match.
            var normalized = amenity.Replace('\u00A0', ' ').ToLowerInvariant();

            foreach (var rule in Rules.Where(rule => rule.Matches(normalized)))
            {
                canonical.Add(rule.Canonical);
            }
        }

        return [.. canonical];
    }

    private sealed record AmenityRule(string Canonical, string[] Includes, string[] Excludes)
    {
        public bool Matches(string amenity) =>
            !Array.Exists(Excludes, exclude => amenity.Contains(exclude, StringComparison.Ordinal)) &&
            Array.Exists(Includes, include => amenity.Contains(include, StringComparison.Ordinal));
    }
}
