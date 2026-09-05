using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Nestly.Domain;
using Nestly.Search.Indexing;

namespace Nestly.Search.Querying;

/// <summary>
/// Builds the facet aggregations, one per dimension, each counted with every filter except its
/// own.
/// </summary>
/// <remarks>
/// The shape is <c>global → filter → terms</c>. <c>global</c> escapes the search's own query so
/// the facet can re-apply a different set of filters; without it every facet would inherit the
/// full query and count only what is already selected — pick Brooklyn and the other four boroughs
/// read zero, so nobody can widen a search without clearing it first.
/// <para>
/// Amenities are the exception, because that filter is conjunctive. Its own dimension stays
/// applied, so a count reads "how many of my current results also have this" — the number that
/// answers whether ticking it is worth it. Excluding it, as the other facets do, would show the
/// whole index's totals beside a filtered result list.
/// </para>
/// </remarks>
public static class ListingFacetAggregations
{
    public const string Boroughs = "boroughs";
    public const string Neighborhoods = "neighborhoods";
    public const string Bedrooms = "bedrooms";
    public const string RoomTypes = "roomTypes";
    public const string PropertyTypes = "propertyTypes";
    public const string Amenities = "amenities";
    public const string Rent = "rent";

    /// <summary>Name of the filter sub-aggregation inside each facet's global wrapper.</summary>
    public const string Filtered = "filtered";

    /// <summary>Name of the value sub-aggregation inside the filter.</summary>
    public const string Values = "values";

    public const string RentHistogram = "histogram";
    public const string RentMin = "min";
    public const string RentMax = "max";

    /// <summary>Bucket width for the rent histogram behind the price slider, in dollars.</summary>
    public const double RentBucketSize = 1000;

    // Sizes are the facet lists the UI can show without a scrollbar of its own. Neighborhoods is
    // the only one with a long tail -- 193 of them -- so it gets the largest list.
    private const int BoroughSize = 5;
    private const int NeighborhoodSize = 25;
    private const int BedroomSize = 10;
    private const int RoomTypeSize = 10;
    private const int PropertyTypeSize = 15;
    private const int AmenitySize = 20;

    public static IDictionary<string, Aggregation> Build(string? query, ListingFilters filters)
    {
        return new Dictionary<string, Aggregation>(StringComparer.Ordinal)
        {
            [Boroughs] = Facet(query, filters, FilterDimension.Borough, Terms(ListingFields.Borough, BoroughSize)),
            [Neighborhoods] = Facet(query, filters, FilterDimension.Neighborhood, Terms(ListingFields.Neighborhood, NeighborhoodSize)),
            [Bedrooms] = Facet(query, filters, FilterDimension.Bedrooms, Terms(ListingFields.Bedrooms, BedroomSize, ascending: true)),
            [RoomTypes] = Facet(query, filters, FilterDimension.RoomType, Terms(ListingFields.RoomType, RoomTypeSize)),
            [PropertyTypes] = Facet(query, filters, FilterDimension.PropertyType, Terms(ListingFields.PropertyType, PropertyTypeSize)),
            [Amenities] = Facet(query, filters, excluding: null, Terms(ListingFields.Amenities, AmenitySize)),
            [Rent] = Facet(query, filters, FilterDimension.Rent, RentAggregations()),
        };
    }

    private static Aggregation Facet(
        string? query,
        ListingFilters filters,
        FilterDimension? excluding,
        Dictionary<string, Aggregation> values) =>
        new()
        {
            Global = new GlobalAggregation(),
            Aggregations = new Dictionary<string, Aggregation>(StringComparer.Ordinal)
            {
                [Filtered] = new Aggregation
                {
                    Filter = ListingQueryBuilder.Build(query, filters, excluding),
                    Aggregations = values,
                },
            },
        };

    private static Dictionary<string, Aggregation> Terms(string field, int size, bool ascending = false)
    {
        var terms = new TermsAggregation { Field = field, Size = size };

        if (ascending)
        {
            // Bedroom counts are a scale, not a popularity list: studio, 1, 2 reads better than
            // 1, 2, studio.
            terms.Order = [new KeyValuePair<Field, SortOrder>("_key", SortOrder.Asc)];
        }

        return new Dictionary<string, Aggregation>(StringComparer.Ordinal) { [Values] = new Aggregation { Terms = terms } };
    }

    private static Dictionary<string, Aggregation> RentAggregations() =>
        new(StringComparer.Ordinal)
        {
            // Min and max give the slider its bounds, and they exclude the rent filter so
            // dragging one handle does not shrink the track under the other.
            [RentMin] = new Aggregation { Min = new MinAggregation { Field = ListingFields.MonthlyRent } },
            [RentMax] = new Aggregation { Max = new MaxAggregation { Field = ListingFields.MonthlyRent } },
            [RentHistogram] = new Aggregation
            {
                Histogram = new HistogramAggregation
                {
                    Field = ListingFields.MonthlyRent,
                    Interval = RentBucketSize,
                    MinDocCount = 0,
                },
            },
        };
}
