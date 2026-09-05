namespace Nestly.Search.Searching;

/// <summary>Just the title, for autocomplete rows.</summary>
internal sealed record ListingTitleDocument
{
    public string Title { get; init; } = string.Empty;
}
