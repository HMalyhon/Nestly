using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Domain;
using Nestly.Search.Configuration;
using Nestly.Search.Indexing;

namespace Nestly.Search.Searching;

internal sealed partial class ListingDetailService : IListingDetailService
{
    private const int NotFoundStatus = 404;

    private readonly ElasticsearchClient _client;
    private readonly ILogger<ListingDetailService> _logger;
    private readonly string _indexName;

    public ListingDetailService(
        ElasticsearchClient client,
        IOptions<ElasticsearchOptions> options,
        ILogger<ListingDetailService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _client = client;
        _logger = logger;
        _indexName = options.Value.IndexName;
    }

    public async Task<Listing?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        // The document API, not a search: the seeder indexes each listing under its own id, so
        // this is a direct read that skips scoring and the query phase entirely.
        var response = await _client.GetAsync<Listing>(
            id,
            get => get
                .Index(_indexName)

                // 384 floats the detail card has no use for.
                .SourceExcludes(ListingFields.DescriptionVector),
            cancellationToken).ConfigureAwait(false);

        // A missing document is a 404 from Elasticsearch, which is an answer rather than a
        // failure -- checked before IsValidResponse, which does not agree.
        if (response.ApiCallDetails.HttpStatusCode == NotFoundStatus)
        {
            return null;
        }

        if (!response.IsValidResponse)
        {
            response.TryGetOriginalException(out var cause);
            LogFailure(response.DebugInformation);

            var rejected = response.ApiCallDetails.HttpStatusCode is >= 400 and < 500;

            throw new SearchException(
                rejected ? "Elasticsearch rejected the lookup." : "Elasticsearch is unavailable.",
                rejected,
                cause);
        }

        return response.Found ? response.Source : null;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Elasticsearch did not answer the lookup: {Details}")]
    private partial void LogFailure(string details);
}
