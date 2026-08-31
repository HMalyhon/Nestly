namespace Nestly.Domain;

/// <summary>
/// The map's payload. Deliberately separate from <see cref="ListingSearchResponse"/> so panning
/// the map never re-transfers descriptions, amenities and vectors for every visible listing.
/// </summary>
/// <remarks>
/// Below <see cref="PinLimit"/> matching listings the response carries individual
/// <see cref="Pins"/>; above it, Elasticsearch aggregates server-side into
/// <see cref="Clusters"/> and the UI draws density bubbles instead. Shipping tens of thousands
/// of markers to Leaflet is what makes these maps feel broken.
/// </remarks>
public sealed record MapResponse
{
    public const int PinLimit = 500;

    public required long Total { get; init; }

    public IReadOnlyList<MapPin> Pins { get; init; } = [];

    public IReadOnlyList<MapCluster> Clusters { get; init; } = [];

    /// <summary>True when the result was too large for pins and was aggregated instead.</summary>
    public required bool IsClustered { get; init; }
}
