using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Microsoft.Extensions.Options;
using Nestly.Domain;
using Nestly.Search.Configuration;
using Nestly.Search.Indexing;
using Nestly.Search.Querying;

namespace Nestly.Search.Searching;

internal sealed class ListingMapService : IListingMapService
{
    private const string GridAggregation = "grid";
    private const string CentroidAggregation = "centroid";
    private const string RentAggregation = "rent";

    /// <summary>
    /// Cells the grid may return. Elasticsearch returns the busiest cells first, so a smaller cap
    /// silently drops a sparse tail; the map sends its viewport with every request, which is what
    /// keeps the visible set well inside this.
    /// </summary>
    private const int MaxCells = 500;

    private const double MedianPercentile = 50;

    private readonly ElasticsearchClient _client;
    private readonly string _indexName;

    public ListingMapService(ElasticsearchClient client, IOptions<ElasticsearchOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _client = client;
        _indexName = options.Value.IndexName;
    }

    public async Task<MapResponse> GetAsync(ListingMapRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = ListingQueryBuilder.Build(request.Query, request.Filters);

        // Pins and cells in one request. Which of the two gets used is only known from the total,
        // and asking for the count first would cost a second round trip on every pan.
        var response = await _client.SearchAsync<ListingMapDocument>(
            search => search
                .Indices(_indexName)
                .Size(MapResponse.PinLimit)
                .TrackTotalHits(true)
                .Query(query)
                .SourceIncludes(Fields.FromStrings([ListingFields.Id, ListingFields.Location, ListingFields.MonthlyRent]))
                .Aggregations(Grid(request.Zoom)),
            cancellationToken).ConfigureAwait(false);

        if (!response.IsValidResponse)
        {
            response.TryGetOriginalException(out var cause);
            var rejected = response.ApiCallDetails.HttpStatusCode is >= 400 and < 500;

            throw new SearchException(
                rejected ? "Elasticsearch rejected the map request." : "Elasticsearch is unavailable.",
                rejected,
                cause);
        }

        var clustered = response.Total > MapResponse.PinLimit;

        return new MapResponse
        {
            Total = response.Total,
            IsClustered = clustered,
            Pins = clustered ? [] : [.. response.Documents.Select(ToPin)],
            Clusters = clustered ? Clusters(response.Aggregations) : [],
        };
    }

    private static MapPin ToPin(ListingMapDocument document) =>
        new(document.Id, document.Location.Lat, document.Location.Lon, document.MonthlyRent);

    private static Dictionary<string, Aggregation> Grid(int zoom) =>
        new(StringComparer.Ordinal)
        {
            [GridAggregation] = new Aggregation
            {
                // Two levels finer than the map itself, so a cell is a cluster of nearby pins
                // rather than a bubble covering half the borough.
                GeotileGrid = new GeotileGridAggregation
                {
                    Field = ListingFields.Location,
                    Precision = Math.Clamp(zoom + 2, 1, 29),
                    Size = MaxCells,
                },
                Aggregations = new Dictionary<string, Aggregation>(StringComparer.Ordinal)
                {
                    // The tile key is a cell, not a place; the centroid of the listings inside it
                    // puts the bubble where the listings actually are.
                    [CentroidAggregation] = new Aggregation
                    {
                        GeoCentroid = new GeoCentroidAggregation { Field = ListingFields.Location },
                    },
                    [RentAggregation] = new Aggregation
                    {
                        Percentiles = new PercentilesAggregation
                        {
                            Field = ListingFields.MonthlyRent,
                            Percents = [MedianPercentile],
                        },
                    },
                },
            },
        };

    private static List<MapCluster> Clusters(AggregateDictionary? aggregations)
    {
        if (aggregations is null ||
            !aggregations.TryGetValue(GridAggregation, out var aggregate) ||
            aggregate is not GeotileGridAggregate grid)
        {
            return [];
        }

        var clusters = new List<MapCluster>(grid.Buckets.Count);

        foreach (var bucket in grid.Buckets)
        {
            if (bucket.Aggregations is not { } cell ||
                !cell.TryGetValue(CentroidAggregation, out var centroid) ||
                centroid is not GeoCentroidAggregate { Location: { } location } ||
                !location.TryGetLatitudeLongitude(out var point))
            {
                continue;
            }

            clusters.Add(new MapCluster(point.Lat, point.Lon, bucket.DocCount, MedianRent(cell)));
        }

        return clusters;
    }

    /// <summary>Median rent inside a cell, so a bubble can say what the area costs.</summary>
    private static int MedianRent(AggregateDictionary aggregations) =>
        aggregations.TryGetValue(RentAggregation, out var aggregate) &&
        aggregate is TDigestPercentilesAggregate percentiles &&
        percentiles.Values.FirstOrDefault() is { Value: { } median }
            ? (int)Math.Round(median)
            : 0;
}
