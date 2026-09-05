using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.Options;
using Nestly.Domain;
using Nestly.Search.Configuration;
using Nestly.Search.Indexing;

namespace Nestly.Search.Searching;

internal sealed class ListingSuggestService : IListingSuggestService
{
    private const string NeighborhoodSuggester = "neighborhoods";

    private const int NeighborhoodCount = 5;
    private const int TitleCount = 5;

    private readonly ElasticsearchClient _client;
    private readonly string _indexName;

    public ListingSuggestService(ElasticsearchClient client, IOptions<ElasticsearchOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _client = client;
        _indexName = options.Value.IndexName;
    }

    public async Task<IReadOnlyList<Suggestion>> SuggestAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        // Both halves in one request: the completion suggester for places, and the
        // search_as_you_type field for listing titles.
        var response = await _client.SearchAsync<ListingTitleDocument>(
            search => search
                .Indices(_indexName)
                .Size(TitleCount)
                .Query(TitlePrefix(query))
                .SourceIncludes(ListingFields.Title)
                .Suggest(Neighborhoods(query)),
            cancellationToken).ConfigureAwait(false);

        if (!response.IsValidResponse)
        {
            response.TryGetOriginalException(out var cause);
            var rejected = response.ApiCallDetails.HttpStatusCode is >= 400 and < 500;

            throw new SearchException(
                rejected ? "Elasticsearch rejected the suggest request." : "Elasticsearch is unavailable.",
                rejected,
                cause);
        }

        var neighborhoods = response.Suggest?.GetCompletion(NeighborhoodSuggester) ?? [];

        // Places first: someone typing "bed" is likelier to want Bedford-Stuyvesant than a
        // listing whose title happens to start that way.
        return
        [
            .. neighborhoods
                .SelectMany(suggest => suggest.Options)
                .Select(option => new Suggestion(option.Text, SuggestionKind.Neighborhood)),
            .. response.Documents.Select(document => new Suggestion(document.Title, SuggestionKind.Listing)),
        ];
    }

    private static Query TitlePrefix(string query) =>
        new MultiMatchQuery
        {
            Query = query,
            Type = TextQueryType.BoolPrefix,

            // The shingled sub-fields search_as_you_type builds; matching all three is what makes
            // a half-typed last word behave.
            Fields = Fields.FromStrings(
            [
                ListingFields.TitleSayt,
                $"{ListingFields.TitleSayt}._2gram",
                $"{ListingFields.TitleSayt}._3gram",
            ]),
        };

    private static Suggester Neighborhoods(string query) => new()
    {
        Suggesters = new Dictionary<string, FieldSuggester>(StringComparer.Ordinal)
        {
            [NeighborhoodSuggester] = new FieldSuggester
            {
                Prefix = query,
                Completion = new CompletionSuggester
                {
                    Field = ListingFields.NeighborhoodSuggest,
                    Size = NeighborhoodCount,

                    // Without this the suggester answers per document, so one prefix returns
                    // "Bedford-Stuyvesant" 341 times.
                    SkipDuplicates = true,
                },
            },
        },
    };
}
