namespace Nestly.Search.Indexing;

/// <summary>
/// Field names as they exist in Elasticsearch. The mapping, the query builders and the
/// aggregations all read them from here, so a rename is one edit and a typo is a compile error
/// rather than a query that silently matches nothing.
/// </summary>
public static class ListingFields
{
    public const string Id = "id";
    public const string Title = "title";
    public const string Description = "description";
    public const string DescriptionVector = "descriptionVector";
    public const string Neighborhood = "neighborhood";
    public const string Borough = "borough";
    public const string Location = "location";
    public const string PricePerNight = "pricePerNight";
    public const string MonthlyRent = "monthlyRent";
    public const string Bedrooms = "bedrooms";
    public const string Bathrooms = "bathrooms";
    public const string Accommodates = "accommodates";
    public const string PropertyType = "propertyType";
    public const string RoomType = "roomType";
    public const string Amenities = "amenities";
    public const string ReviewScore = "reviewScore";
    public const string MinimumNights = "minimumNights";
    public const string LastReviewedAt = "lastReviewedAt";

    // Sub-field names, used both to build the mapping and to query it.
    public const string SaytSuffix = "sayt";
    public const string KeywordSuffix = "keyword";
    public const string TextSuffix = "text";
    public const string SuggestSuffix = "suggest";

    /// <summary>Prefix-matches partial words for search-as-you-type on the title.</summary>
    public const string TitleSayt = $"{Title}.{SaytSuffix}";

    /// <summary>Unanalyzed title, for sorting and exact matching.</summary>
    public const string TitleKeyword = $"{Title}.{KeywordSuffix}";

    /// <summary>Analyzed neighborhood, so "east village" matches "East Village" in free text.</summary>
    public const string NeighborhoodText = $"{Neighborhood}.{TextSuffix}";

    /// <summary>Completion suggester over neighborhood names.</summary>
    public const string NeighborhoodSuggest = $"{Neighborhood}.{SuggestSuffix}";
}
