using System.Globalization;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Nestly.Domain;
using Nestly.Search.Indexing;

namespace Nestly.Search.Querying;

/// <summary>
/// Turns a <see cref="ListingSearchRequest"/> into Elasticsearch queries. Pure: no client, no
/// I/O, no state -- which is what makes the generated DSL straightforward to unit-test.
/// </summary>
public static class ListingQueryBuilder
{
    /// <summary>
    /// Free-text fields and their weights. Title counts triple: a listing whose <em>name</em>
    /// says "sunny studio" is a better answer to that query than one whose description mentions
    /// both words in different paragraphs.
    /// </summary>
    private static readonly string[] TextFields =
    [
        $"{ListingFields.Title}^3",
        ListingFields.Description,
        ListingFields.NeighborhoodText,
    ];

    /// <summary>
    /// Builds the query for a search: free text scored, filters not.
    /// </summary>
    /// <param name="query">Free text. Null or blank runs a filters-only browse.</param>
    /// <param name="filters">Structured constraints.</param>
    /// <param name="excluding">
    /// A dimension to leave out, for facet aggregations that must not constrain themselves.
    /// </param>
    public static Query Build(string? query, ListingFilters filters, FilterDimension? excluding = null)
    {
        var clauses = Filters(filters, excluding);
        var text = Text(query);

        if (text is null && clauses.Count == 0)
        {
            return new MatchAllQuery();
        }

        return new BoolQuery
        {
            // Filter rather than Must: filters are yes-or-no, so scoring them would be noise,
            // and the filter context is cacheable in a way the query context is not.
            Filter = clauses.Count == 0 ? null : clauses,
            Must = text is null ? null : [text],
        };
    }

    /// <summary>
    /// The vector half of a hybrid search: approximate nearest neighbours over the description
    /// embedding, constrained by the same filters as the lexical leg.
    /// </summary>
    /// <param name="queryVector">The embedded query, unit length like the indexed vectors.</param>
    /// <param name="filters">Structured constraints, pushed into the kNN filter clause.</param>
    /// <param name="k">Neighbours to return.</param>
    /// <param name="candidates">Neighbours to examine per shard before returning k.</param>
    public static KnnSearch Knn(float[] queryVector, ListingFilters filters, int k, int candidates)
    {
        var clauses = Filters(filters);

        return new KnnSearch
        {
            Field = ListingFields.DescriptionVector,
            QueryVector = queryVector,
            K = k,

            // HNSW is approximate: it examines this many candidates per shard and keeps the best
            // k. Below roughly 2k the recall drops off visibly for no meaningful saving.
            NumCandidates = candidates,

            // The same filters as the lexical leg, and not optional. Without them a vector hit
            // can be a $30,000 Manhattan loft answering a search filtered to cheap Brooklyn
            // studios -- semantically close, and exactly what the user excluded.
            Filter = clauses.Count == 0 ? null : clauses,
        };
    }

    /// <summary>
    /// Fetches exactly these documents, for hydrating a page that fusion has already ordered.
    /// </summary>
    // A terms query on the keyword id rather than the document _id: both work, and this one keeps
    // the hydration expressible in the same typed DSL as everything else.
    public static Query ByIds(IReadOnlyList<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        return new TermsQuery
        {
            Field = ListingFields.Id,
            Terms = new TermsQueryField([.. ids.Select(FieldValue.String)]),
        };
    }

    /// <summary>
    /// The same fields without fuzziness, for highlighting.
    /// </summary>
    /// <remarks>
    /// Re-running the fuzzy rewrite per highlighted document costs ten times what the search
    /// itself does -- 149 ms against 14 ms over one page. A fuzzy match is by definition not the
    /// word that was typed, so leaving it unemphasised is a defensible trade rather than only a
    /// cheap one.
    /// </remarks>
    public static Query? HighlightText(string? query) => Text(query, fuzzy: false);

    /// <summary>The free-text half of a search, or null when there is nothing to match.</summary>
    public static Query? Text(string? query) => Text(query, fuzzy: true);

    public static IList<Query> Filters(ListingFilters filters, FilterDimension? excluding = null)
    {
        ArgumentNullException.ThrowIfNull(filters);

        return [.. FiltersByDimension(filters)
            .Where(entry => entry.Dimension != excluding)
            .Select(entry => entry.Query)];
    }

    /// <summary>Sort order. Relevance is Elasticsearch's default, so it is expressed as no sort at all.</summary>
    public static IList<SortOptions> Sort(ListingSort sort, GeoPoint? near) => sort switch
    {
        ListingSort.PriceAsc => [Ascending(ListingFields.MonthlyRent)],
        ListingSort.PriceDesc => [Descending(ListingFields.MonthlyRent)],

        // Unreviewed listings sort last rather than first: a listing with no score is not a
        // listing with a bad one, but it is not what someone sorting by rating asked to see.
        ListingSort.ReviewScoreDesc => [Descending(ListingFields.ReviewScore, SortMode.Max)],

        // Distance sorting needs an origin. Asking for it without one is a caller error, but a
        // failed search is a poor way to say so, so it falls back to relevance.
        ListingSort.DistanceAsc when near is { } origin => [ByDistance(origin)],
        _ => [],
    };

    private static Query? Text(string? query, bool fuzzy)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        return new MultiMatchQuery
        {
            Query = query,
            Fields = Fields.FromStrings(TextFields),

            // best_fields, not cross_fields: a query like "sunny studio" is looking for one
            // field that says both, rather than a listing that scatters the words across three.
            Type = TextQueryType.BestFields,

            // One typo tolerated on longer words, so a mistyped "brooklin" still finds Brooklyn.
            Fuzziness = fuzzy ? new Fuzziness("AUTO") : null,

            // Fuzzy matching only after the first two characters. Typos in an opening letter are
            // rare, and without this every term walks a wide slice of the term dictionary --
            // which is what made a long query take a second.
            PrefixLength = fuzzy ? 2 : null,
        };
    }

    /// <summary>
    /// The structured constraints, one query per dimension the caller actually set.
    /// </summary>
    private static IEnumerable<(FilterDimension Dimension, Query Query)> FiltersByDimension(ListingFilters filters)
    {
        if (filters.MinRent is not null || filters.MaxRent is not null)
        {
            yield return (FilterDimension.Rent, Range(ListingFields.MonthlyRent, filters.MinRent, filters.MaxRent));
        }

        if (filters.Bedrooms.Count > 0)
        {
            yield return (FilterDimension.Bedrooms, Terms(ListingFields.Bedrooms, filters.Bedrooms.Select(bedrooms => (double)bedrooms)));
        }

        if (filters.MinBathrooms is not null)
        {
            yield return (FilterDimension.Bathrooms, Range(ListingFields.Bathrooms, (double)filters.MinBathrooms.Value, null));
        }

        if (filters.MinAccommodates is not null)
        {
            yield return (FilterDimension.Accommodates, Range(ListingFields.Accommodates, filters.MinAccommodates.Value, null));
        }

        if (filters.Boroughs.Count > 0)
        {
            yield return (FilterDimension.Borough, Terms(ListingFields.Borough, filters.Boroughs));
        }

        if (filters.Neighborhoods.Count > 0)
        {
            yield return (FilterDimension.Neighborhood, Terms(ListingFields.Neighborhood, filters.Neighborhoods));
        }

        if (filters.RoomTypes.Count > 0)
        {
            yield return (FilterDimension.RoomType, Terms(ListingFields.RoomType, filters.RoomTypes));
        }

        if (filters.PropertyTypes.Count > 0)
        {
            yield return (FilterDimension.PropertyType, Terms(ListingFields.PropertyType, filters.PropertyTypes));
        }

        if (filters.Amenities.Count > 0)
        {
            // Conjunctive, unlike every other keyword filter: ticking "Elevator" and "Washer"
            // asks for listings with both, not either. Selecting more amenities should narrow
            // the results, which a terms query would not do.
            yield return (FilterDimension.Amenities, new BoolQuery
            {
                Filter = [.. filters.Amenities.Select(amenity => Term(ListingFields.Amenities, amenity))],
            });
        }

        if (filters.MinReviewScore is not null)
        {
            yield return (FilterDimension.ReviewScore, Range(ListingFields.ReviewScore, filters.MinReviewScore.Value, null));
        }

        foreach (var geo in GeoFilters(filters))
        {
            yield return (FilterDimension.Geo, geo);
        }
    }

    private static IEnumerable<Query> GeoFilters(ListingFilters filters)
    {
        if (filters.Near is { } near && filters.RadiusKm is { } radius)
        {
            yield return new GeoDistanceQuery
            {
                Field = ListingFields.Location,
                Distance = string.Create(CultureInfo.InvariantCulture, $"{radius:0.###}km"),
                Location = GeoLocation.LatitudeLongitude(new LatLonGeoLocation { Lat = near.Lat, Lon = near.Lon }),
            };
        }

        // Applied on top of any radius rather than instead of it: the viewport is what the user
        // can see, the radius is what they asked for, and both are true at once.
        if (filters.Within is { } bounds)
        {
            yield return new GeoBoundingBoxQuery
            {
                Field = ListingFields.Location,
                BoundingBox = Elastic.Clients.Elasticsearch.GeoBounds.TopLeftBottomRight(new TopLeftBottomRightGeoBounds
                {
                    TopLeft = GeoLocation.LatitudeLongitude(new LatLonGeoLocation { Lat = bounds.TopLat, Lon = bounds.LeftLon }),
                    BottomRight = GeoLocation.LatitudeLongitude(new LatLonGeoLocation { Lat = bounds.BottomLat, Lon = bounds.RightLon }),
                }),
            };
        }
    }

    private static Query Range(string field, double? min, double? max) =>
        new NumberRangeQuery(field) { Gte = min, Lte = max };

    private static Query Terms(string field, IEnumerable<string> values) =>
        new TermsQuery { Field = field, Terms = new TermsQueryField([.. values.Select(FieldValue.String)]) };

    private static Query Terms(string field, IEnumerable<double> values) =>
        new TermsQuery { Field = field, Terms = new TermsQueryField([.. values.Select(value => FieldValue.Double(value))]) };

    private static Query Term(string field, string value) =>
        new TermQuery { Field = field, Value = FieldValue.String(value) };

    private static SortOptions Ascending(string field) =>
        new FieldSort { Field = field, Order = SortOrder.Asc };

    private static SortOptions Descending(string field, SortMode? mode = null) =>
        new FieldSort { Field = field, Order = SortOrder.Desc, Mode = mode, Missing = "_last" };

    private static SortOptions ByDistance(GeoPoint origin) =>
        new GeoDistanceSort
        {
            Field = ListingFields.Location,
            Location = [GeoLocation.LatitudeLongitude(new LatLonGeoLocation { Lat = origin.Lat, Lon = origin.Lon })],
            Order = SortOrder.Asc,
            Unit = DistanceUnit.Kilometers,
        };
}
