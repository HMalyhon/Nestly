namespace Nestly.Search.Querying;

/// <summary>
/// The facetable dimensions a search can be narrowed by.
/// </summary>
/// <remarks>
/// Filters are built per dimension rather than as one flat list, because facet counts have to be
/// computed with every filter applied <em>except</em> the one whose values they are counting. A
/// borough facet computed with the borough filter applied would report the selected borough and
/// zero for everything else, which is how a filter list turns into a dead end.
/// </remarks>
public enum FilterDimension
{
    Rent,
    Bedrooms,
    Bathrooms,
    Accommodates,
    Borough,
    Neighborhood,
    RoomType,
    PropertyType,
    Amenities,
    ReviewScore,

    /// <summary>Radius and map viewport together: both constrain where, and neither is faceted.</summary>
    Geo,
}
