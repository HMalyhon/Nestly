namespace Nestly.Domain;

/// <summary>Everything the UI can ask for in one search.</summary>
public sealed record ListingSearchRequest
{
    /// <summary>Free text. Empty or null runs a filters-only browse.</summary>
    public string? Query { get; init; }

    public ListingFilters Filters { get; init; } = new();

    public ListingSort Sort { get; init; } = ListingSort.Relevance;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
