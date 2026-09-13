namespace Nestly.Search.Searching;

/// <summary>Retrieval limits that the request contract has to agree with.</summary>
// Public because validation happens at the API boundary, one assembly up, and a limit enforced
// there against a number guessed here is the kind of pair that drifts apart silently.
public static class SearchLimits
{
    /// <summary>How many fused results a text search can page through.</summary>
    // The lexical leg is capped at this, so a page starting past it has nothing left to slice.
    public const int MaxFusedResults = 2_000;
}
