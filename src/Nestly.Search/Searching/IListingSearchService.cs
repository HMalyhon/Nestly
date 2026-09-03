using Nestly.Domain;

namespace Nestly.Search.Searching;

/// <summary>Runs listing searches. The API layer holds no Elasticsearch knowledge of its own.</summary>
public interface IListingSearchService
{
    Task<ListingSearchResponse> SearchAsync(ListingSearchRequest request, CancellationToken cancellationToken = default);
}
