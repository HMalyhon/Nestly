namespace Nestly.Search.Indexing;

/// <summary>Creates and drops the <c>listings</c> index.</summary>
public interface IListingIndexProvisioner
{
    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops the index and creates it again, so a re-seed starts from a known-empty index with
    /// the current mapping rather than layering documents onto a stale one.
    /// </summary>
    Task RecreateAsync(CancellationToken cancellationToken = default);
}
