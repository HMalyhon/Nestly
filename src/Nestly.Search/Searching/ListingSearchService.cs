using System.Diagnostics;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Microsoft.Extensions.Options;
using Nestly.Domain;
using Nestly.Search.Configuration;
using Nestly.Search.Indexing;
using Nestly.Search.Querying;

namespace Nestly.Search.Searching;

internal sealed class ListingSearchService : IListingSearchService
{
    // Two snippets of roughly a sentence each: what fits on a result card.
    private const int FragmentSize = 160;
    private const int FragmentCount = 2;

    private readonly ElasticsearchClient _client;
    private readonly string _indexName;

    public ListingSearchService(ElasticsearchClient client, IOptions<ElasticsearchOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _client = client;
        _indexName = options.Value.IndexName;
    }

    public async Task<ListingSearchResponse> SearchAsync(
        ListingSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch = Stopwatch.StartNew();
        var from = (request.Page - 1) * request.PageSize;

        var response = await _client.SearchAsync<Listing>(
            search =>
            {
                search
                    .Indices(_indexName)
                    .From(from)
                    .Size(request.PageSize)
                    .Query(ListingQueryBuilder.Build(request.Query, request.Filters))
                    .Sort(ListingQueryBuilder.Sort(request.Sort, request.Filters.Near))

                    // 384 floats per document, useless to a result card.
                    .SourceExcludes(ListingFields.DescriptionVector);

                if (!string.IsNullOrWhiteSpace(request.Query))
                {
                    search.Highlight(Highlight());
                }
            },
            cancellationToken).ConfigureAwait(false);

        if (!response.IsValidResponse)
        {
            response.TryGetOriginalException(out var cause);
            throw new InvalidOperationException($"Listing search failed: {response.DebugInformation}", cause);
        }

        stopwatch.Stop();

        return new ListingSearchResponse
        {
            Total = response.Total,
            Hits = [.. response.Hits.Select(hit => ToHit(hit, request.Filters.Near))],
            Facets = new ListingFacets(),
            ElapsedMs = stopwatch.ElapsedMilliseconds,
        };
    }

    private static ListingHit ToHit(Hit<Listing> hit, GeoPoint? origin)
    {
        var listing = hit.Source!;

        return new ListingHit
        {
            Listing = listing,
            Score = hit.Score ?? 0,
            Highlights = hit.Highlight?.GetValueOrDefault(ListingFields.Description) is { } fragments
                ? [.. fragments]
                : [],

            // Lexical only until the vector leg lands.
            MatchedBy = MatchSource.Lexical,

            // Cheaper here than as a script field: the coordinates are already in the response.
            DistanceKm = origin is { } from ? GeoDistance.Kilometers(from, listing.Location) : null,
        };
    }

    private static Highlight Highlight() => new()
    {
        Fields = new Dictionary<Field, HighlightField>
        {
            [ListingFields.Description] = new()
            {
                FragmentSize = FragmentSize,
                NumberOfFragments = FragmentCount,

                // Reads stored offsets instead of re-analysing the field.
                Type = HighlighterType.Unified,
            },
            [ListingFields.Title] = new() { NumberOfFragments = 0 },
        },
    };
}
