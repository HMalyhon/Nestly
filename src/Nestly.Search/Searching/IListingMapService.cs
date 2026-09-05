using Nestly.Domain;

namespace Nestly.Search.Searching;

/// <summary>Answers the map pane: pins while they fit, density cells when they do not.</summary>
public interface IListingMapService
{
    Task<MapResponse> GetAsync(ListingMapRequest request, CancellationToken cancellationToken = default);
}
