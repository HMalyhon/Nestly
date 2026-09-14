using System.Diagnostics;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.MSearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Transport.Products.Elasticsearch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Domain;
using Nestly.Search.Configuration;
using Nestly.Search.Embedding;
using Nestly.Search.Indexing;
using Nestly.Search.Querying;
using Nestly.Search.Ranking;

namespace Nestly.Search.Searching;

internal sealed partial class ListingSearchService : IListingSearchService
{
    // Two snippets of roughly a sentence each: what fits on a result card.
    private const int FragmentSize = 160;
    private const int FragmentCount = 2;

    /// <summary>How deep each retrieval leg goes before the two are fused.</summary>
    // Deep enough that a document ranked mid-table by one leg can still be lifted by the other,
    // shallow enough that a keystroke does not ask the cluster for a thousand rows.
    private const int FusionDepth = 100;

    /// <summary>Ceiling on the lexical leg when a deep page is requested.</summary>
    // Page 100 of 20 needs 2,000 ranked ids to slice from. Past the fused window the vector leg
    // has nothing left to contribute, so those pages come out in lexical order -- which falls out
    // of the arithmetic rather than needing a branch: a document present in one list scores
    // 1/(k+rank), and that ordering is the lexical ordering. A request for a page past it is
    // refused by the validator rather than answered here, so this is a ceiling and not a clamp.
    private const int MaxRetrieval = SearchLimits.MaxFusedResults;

    private readonly ElasticsearchClient _client;
    private readonly IListingEmbedder _embedder;
    private readonly ILogger<ListingSearchService> _logger;
    private readonly string _indexName;

    public ListingSearchService(
        ElasticsearchClient client,
        IListingEmbedder embedder,
        IOptions<ElasticsearchOptions> options,
        ILogger<ListingSearchService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _client = client;
        _embedder = embedder;
        _logger = logger;
        _indexName = options.Value.IndexName;
    }

    public async Task<ListingSearchResponse> SearchAsync(
        ListingSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch = Stopwatch.StartNew();

        // Nothing to embed and nothing to fuse: a filters-only browse is one search and a sort.
        var result = string.IsNullOrWhiteSpace(request.Query)
            ? await BrowseAsync(request, cancellationToken).ConfigureAwait(false)
            : await HybridAsync(request, cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();

        return new ListingSearchResponse
        {
            Total = result.Total,
            Hits = result.Hits,
            Facets = result.Facets,
            ElapsedMs = stopwatch.ElapsedMilliseconds,
        };
    }

    /// <summary>
    /// Snippets for one page of results.
    /// </summary>
    // Applied when the page is hydrated, not when the legs retrieve: highlighting the hundred
    // documents a leg ranks in order to show twenty costs 292 ms against 14 ms, and doing it here
    // also covers the hits that only the vector leg found, which the lexical leg never saw.
    private static Highlight Highlight(string? query) => new()
    {
        // Escapes the field text, so the only markup in a fragment is the highlighter's own
        // <em>. Without it a description containing markup would be handed to a browser intact.
        Encoder = HighlighterEncoder.Html,

        Fields = new Dictionary<Field, HighlightField>
        {
            [ListingFields.Description] = new()
            {
                FragmentSize = FragmentSize,
                NumberOfFragments = FragmentCount,

                // The page was chosen by id, so the highlighter is told separately what to look
                // for -- without this it would find nothing to emphasise.
                HighlightQuery = ListingQueryBuilder.HighlightText(query),

                // Unified is the default; named because it is the one that handles phrase
                // and fuzzy matches correctly, which the plain highlighter does not.
                Type = HighlighterType.Unified,
            },
        },
    };

    private static IReadOnlyList<string> Ranking<T>(IReadOnlyCollection<Hit<T>> hits) =>
        [.. hits.Select(hit => hit.Id).OfType<string>()];

    private static IReadOnlyList<string> Fragments(IReadOnlyDictionary<string, IReadOnlyCollection<string>>? highlight) =>
        highlight?.GetValueOrDefault(ListingFields.Description) is { } fragments ? [.. fragments] : [];

    private static ListingHit ToHit(
        Listing listing,
        MatchSource matchedBy,
        double score,
        IReadOnlyList<string> highlights,
        GeoPoint? origin) => new()
        {
            Listing = listing,
            Score = score,
            Highlights = highlights,
            MatchedBy = matchedBy,

            // Cheaper here than as a script field: the coordinates are already in the response.
            DistanceKm = origin is { } from ? GeoDistance.Kilometers(from, listing.Location) : null,
        };

    /// <summary>The one field a sort reads, or no source at all when ranking by relevance.</summary>
    private static SourceConfig SortSource(ListingSort sort)
    {
        var field = sort switch
        {
            ListingSort.PriceAsc or ListingSort.PriceDesc => ListingFields.MonthlyRent,
            ListingSort.ReviewScoreDesc => ListingFields.ReviewScore,
            ListingSort.DistanceAsc => ListingFields.Location,
            _ => null,
        };

        return field is null
            ? new SourceConfig(false)
            : new SourceConfig(new Elastic.Clients.Elasticsearch.Core.Search.SourceFilter
            {
                Includes = Fields.FromStrings([field]),
            });
    }

    /// <summary>Orders the fused set by the requested field, leaving relevance order untouched.</summary>
    // Elasticsearch sorts each leg separately, so it cannot order the union the legs produce;
    // without this the sort was accepted and then silently ignored on every query with text in it.
    // OrderBy is stable, so ties keep their fused order and a page stays the same page.
    private static IReadOnlyList<FusedHit> Reorder(
        IReadOnlyList<FusedHit> fused,
        ListingSort sort,
        GeoPoint? near,
        IReadOnlyCollection<Hit<ListingSortDocument>> lexical,
        IReadOnlyCollection<Hit<ListingSortDocument>> vector)
    {
        if (sort == ListingSort.Relevance)
        {
            return fused;
        }

        var values = new Dictionary<string, ListingSortDocument>(StringComparer.Ordinal);

        foreach (var hit in lexical.Concat(vector))
        {
            if (hit.Id is { } id && hit.Source is { } document)
            {
                values[id] = document;
            }
        }

        return sort switch
        {
            ListingSort.PriceAsc => [.. fused.OrderBy(hit => Rent(hit, values))],
            ListingSort.PriceDesc => [.. fused.OrderByDescending(hit => Rent(hit, values))],

            // Unreviewed listings sort last rather than first, as they do in ListingQueryBuilder.
            ListingSort.ReviewScoreDesc => [.. fused.OrderByDescending(hit => Score(hit, values))],
            ListingSort.DistanceAsc when near is { } origin =>
                [.. fused.OrderBy(hit => Distance(hit, values, origin))],
            _ => fused,
        };

        static int Rent(FusedHit hit, Dictionary<string, ListingSortDocument> values) =>
            values.TryGetValue(hit.Id, out var document) ? document.MonthlyRent : int.MaxValue;

        static double Score(FusedHit hit, Dictionary<string, ListingSortDocument> values) =>
            values.TryGetValue(hit.Id, out var document) && document.ReviewScore is { } score
                ? score
                : double.NegativeInfinity;

        static double Distance(FusedHit hit, Dictionary<string, ListingSortDocument> values, GeoPoint origin) =>
            values.TryGetValue(hit.Id, out var document) && document.Location is { } location
                ? GeoDistance.Kilometers(origin, location)
                : double.PositiveInfinity;
    }

    /// <summary>Filters-only browse: one search, sorted, paged by Elasticsearch.</summary>
    private async Task<SearchResult> BrowseAsync(ListingSearchRequest request, CancellationToken cancellationToken)
    {
        var from = (request.Page - 1) * request.PageSize;

        var response = await _client.SearchAsync<Listing>(
            search => search
                .Indices(_indexName)
                .From(from)
                .Size(request.PageSize)

                // Elasticsearch stops counting at 10,000 by default and reports the total as a
                // lower bound, which this response presents as exact.
                .TrackTotalHits(true)
                .Query(ListingQueryBuilder.Build(request.Query, request.Filters))
                .Sort(ListingQueryBuilder.Sort(request.Sort, request.Filters.Near))

                // 384 floats per document, useless to a result card.
                .SourceExcludes(ListingFields.DescriptionVector)

                // Facets ride along on the same request: a second round trip to count what this
                // one already matched would double the latency the UI feels.
                .Aggregations(ListingFacetAggregations.Build(request.Query, request.Filters)),
            cancellationToken).ConfigureAwait(false);

        Ensure(response, "browse");

        return new SearchResult(
            response.Total,
            [.. response.Hits.Select(hit => ToHit(hit.Source!, MatchSource.Lexical, hit.Score ?? 0, [], request.Filters.Near))],
            FacetReader.Read(response.Aggregations));
    }

    /// <summary>BM25 and kNN together, fused by rank, then the requested page hydrated.</summary>
    // The two legs go out concurrently rather than as one _msearch: the 9.x client exposes no way
    // to build msearch bodies, and hand-rolling the ndjson would be worse code than this for a
    // saving of one round trip that Task.WhenAll already hides behind the slower leg.
    private async Task<SearchResult> HybridAsync(ListingSearchRequest request, CancellationToken cancellationToken)
    {
        var from = (request.Page - 1) * request.PageSize;
        var depth = Math.Clamp(from + request.PageSize, FusionDepth, MaxRetrieval);
        var queryVector = _embedder.Embed(request.Query!);

        // Ids and scores only, plus the one field a non-relevance sort orders by. The documents
        // for the page that survives fusion are fetched afterwards.
        var source = SortSource(request.Sort);

        var lexicalLeg = _client.SearchAsync<ListingSortDocument>(
            search => search
                .Indices(_indexName)
                .Query(ListingQueryBuilder.Build(request.Query, request.Filters))
                .Size(depth)

                // As in BrowseAsync: the count this leg reports is the one the response carries.
                .TrackTotalHits(true)
                .Source(source)

                // Facets ride on this leg: they describe what the filters and text match, which
                // is exactly what the lexical query already had to compute.
                .Aggregations(ListingFacetAggregations.Build(request.Query, request.Filters)),
            cancellationToken);

        var vectorLeg = _client.SearchAsync<ListingSortDocument>(
            search => search
                .Indices(_indexName)
                .Knn(ListingQueryBuilder.Knn(queryVector, request.Filters, FusionDepth, FusionDepth * 2))
                .Size(FusionDepth)
                .Source(source),
            cancellationToken);

        await Task.WhenAll(lexicalLeg, vectorLeg).ConfigureAwait(false);

        var lexical = await lexicalLeg.ConfigureAwait(false);
        var vector = await vectorLeg.ConfigureAwait(false);

        Ensure(lexical, "search");
        Ensure(vector, "vector search");

        var fused = RrfFusion.Fuse(Ranking(lexical.Hits), Ranking(vector.Hits));
        var ordered = Reorder(fused, request.Sort, request.Filters.Near, lexical.Hits, vector.Hits);
        var page = ordered.Skip(from).Take(request.PageSize).ToArray();

        var documents = await HydrateAsync(
            [.. page.Select(hit => hit.Id)],
            request.Query,
            cancellationToken).ConfigureAwait(false);

        // The hits come from the fused set, so the lexical count alone can be smaller than the list
        // it is describing: a vector-only hit is by definition one the lexical leg never returned.
        var total = Math.Max(lexical.Total, fused.Count);

        return new SearchResult(
            total,
            [
                .. page
                    .Where(hit => documents.ContainsKey(hit.Id))
                    .Select(hit => ToHit(
                        documents[hit.Id].Listing,
                        hit.MatchedBy,
                        hit.Score,
                        documents[hit.Id].Highlights,
                        request.Filters.Near)),
            ],
            FacetReader.Read(lexical.Aggregations));
    }

    /// <summary>Fetches the documents for one page of fused ids, in one round trip.</summary>
    private async Task<Dictionary<string, Hydrated>> HydrateAsync(
        IReadOnlyList<string> ids,
        string? query,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var response = await _client.SearchAsync<Listing>(
            search => search
                .Indices(_indexName)
                .Size(ids.Count)
                .Query(ListingQueryBuilder.ByIds(ids))
                .SourceExcludes(ListingFields.DescriptionVector)
                .Highlight(Highlight(query)),
            cancellationToken).ConfigureAwait(false);

        Ensure(response, "hydrate");

        // Keyed, not ordered: the order that matters is the fused one, applied by the caller.
        return response.Hits
            .Where(hit => hit.Source is not null)
            .ToDictionary(
                hit => hit.Source!.Id,
                hit => new Hydrated(hit.Source!, Fragments(hit.Highlight)),
                StringComparer.Ordinal);
    }

    private void Ensure(ElasticsearchResponse response, string what)
    {
        if (response.IsValidResponse)
        {
            return;
        }

        response.TryGetOriginalException(out var cause);

        // DebugInformation holds the cluster address and the generated DSL. It belongs in the
        // logs, not in an exception message that an error handler might render to a client.
        LogFailure(what, response.DebugInformation);

        var rejected = response.ApiCallDetails.HttpStatusCode is >= 400 and < 500;

        throw new SearchException(
            rejected ? $"Elasticsearch rejected the {what} request." : "Elasticsearch is unavailable.",
            rejected,
            cause);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Elasticsearch did not answer the {What}: {Details}")]
    private partial void LogFailure(string what, string details);

    private readonly record struct SearchResult(long Total, IReadOnlyList<ListingHit> Hits, ListingFacets Facets);

    private readonly record struct Hydrated(Listing Listing, IReadOnlyList<string> Highlights);
}
