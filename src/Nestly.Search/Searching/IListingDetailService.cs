using Nestly.Domain;

namespace Nestly.Search.Searching;

/// <summary>Reads a single listing by its identifier.</summary>
public interface IListingDetailService
{
    /// <summary>Returns the listing, or null when no document carries that identifier.</summary>
    Task<Listing?> GetAsync(string id, CancellationToken cancellationToken = default);
}
