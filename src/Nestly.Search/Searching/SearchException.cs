namespace Nestly.Search.Searching;

/// <summary>A search that Elasticsearch did not answer.</summary>
// Split by whose fault it is, because the two need different status codes: a query the cluster
// rejects is the caller's problem, an unreachable cluster is ours.
public sealed class SearchException(string message, bool causedByRequest, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>True when Elasticsearch rejected the request; false when it could not be reached.</summary>
    public bool CausedByRequest { get; } = causedByRequest;
}
