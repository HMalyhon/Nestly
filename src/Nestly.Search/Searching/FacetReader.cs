using System.Globalization;
using Elastic.Clients.Elasticsearch.Aggregations;
using Nestly.Domain;
using Nestly.Search.Querying;

namespace Nestly.Search.Searching;

/// <summary>Reads the facet aggregations back out of a search response.</summary>
internal static class FacetReader
{
    public static ListingFacets Read(AggregateDictionary? aggregations)
    {
        if (aggregations is null)
        {
            return new ListingFacets();
        }

        var rent = Filtered(aggregations, ListingFacetAggregations.Rent);

        return new ListingFacets
        {
            Boroughs = Buckets(aggregations, ListingFacetAggregations.Boroughs),
            Neighborhoods = Buckets(aggregations, ListingFacetAggregations.Neighborhoods),
            Bedrooms = Buckets(aggregations, ListingFacetAggregations.Bedrooms),
            RoomTypes = Buckets(aggregations, ListingFacetAggregations.RoomTypes),
            PropertyTypes = Buckets(aggregations, ListingFacetAggregations.PropertyTypes),
            Amenities = Buckets(aggregations, ListingFacetAggregations.Amenities),
            RentHistogram = Histogram(rent),
            MinRent = Bound(rent, ListingFacetAggregations.RentMin),
            MaxRent = Bound(rent, ListingFacetAggregations.RentMax),
        };
    }

    /// <summary>Unwraps the <c>global → filter</c> pair every facet is nested in.</summary>
    private static AggregateDictionary? Filtered(AggregateDictionary aggregations, string name) =>
        aggregations.TryGetValue(name, out var aggregate) &&
        aggregate is GlobalAggregate { Aggregations: { } inner } &&
        inner.TryGetValue(ListingFacetAggregations.Filtered, out var filtered) &&
        filtered is FilterAggregate filter
            ? filter.Aggregations
            : null;

    private static IReadOnlyList<FacetBucket> Buckets(AggregateDictionary aggregations, string name)
    {
        var values = Filtered(aggregations, name);

        if (values is null || !values.TryGetValue(ListingFacetAggregations.Values, out var aggregate))
        {
            return [];
        }

        // Keyword fields come back as string terms and numeric fields as long terms, so bedrooms
        // needs the second shape while every other facet needs the first.
        return aggregate switch
        {
            StringTermsAggregate strings =>
                [.. strings.Buckets.Select(bucket => new FacetBucket(bucket.Key.ToString(), bucket.DocCount))],
            LongTermsAggregate longs =>
                [.. longs.Buckets.Select(bucket => new FacetBucket(bucket.Key.ToString(CultureInfo.InvariantCulture), bucket.DocCount))],
            _ => [],
        };
    }

    private static IReadOnlyList<FacetBucket> Histogram(AggregateDictionary? rent)
    {
        if (rent is null ||
            !rent.TryGetValue(ListingFacetAggregations.RentHistogram, out var aggregate) ||
            aggregate is not HistogramAggregate histogram)
        {
            return [];
        }

        return
        [
            .. histogram.Buckets.Select(bucket =>
                new FacetBucket(bucket.Key.ToString(CultureInfo.InvariantCulture), bucket.DocCount)),
        ];
    }

    private static int? Bound(AggregateDictionary? rent, string name)
    {
        if (rent is null || !rent.TryGetValue(name, out var aggregate))
        {
            return null;
        }

        var value = aggregate switch
        {
            MinAggregate min => min.Value,
            MaxAggregate max => max.Value,
            _ => null,
        };

        return value is null ? null : (int)Math.Round(value.Value);
    }
}
