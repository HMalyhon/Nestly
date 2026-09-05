using Elastic.Clients.Elasticsearch;
using Elastic.Transport.Products.Elasticsearch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Search.Configuration;

namespace Nestly.Search.Indexing;

internal sealed partial class ListingIndexProvisioner : IListingIndexProvisioner
{
    private readonly ElasticsearchClient _client;
    private readonly ILogger<ListingIndexProvisioner> _logger;
    private readonly IndexName _index;

    // Kept alongside the IndexName because IndexName.ToString() allocates, and the logging
    // analyzer is right to object to that happening on a log level that may be disabled.
    private readonly string _indexName;

    public ListingIndexProvisioner(
        ElasticsearchClient client,
        IOptions<ElasticsearchOptions> options,
        ILogger<ListingIndexProvisioner> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _client = client;
        _logger = logger;
        _index = options.Value.IndexName;
        _indexName = options.Value.IndexName;
    }

    public async Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _client.Indices.ExistsAsync(_index, cancellationToken).ConfigureAwait(false);
        return response.Exists;
    }

    public async Task RecreateAsync(CancellationToken cancellationToken = default)
    {
        if (await ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            var deleted = await _client.Indices.DeleteAsync(_index, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(deleted, $"delete index '{_indexName}'");
            LogDeleted(_indexName);
        }

        await CreateAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureSuccess(ElasticsearchResponse response, string operation)
    {
        if (response.IsValidResponse)
        {
            return;
        }

        // DebugInformation carries the server's own error body. Losing it here would leave a
        // failure reading "could not create index" with no mapping error to act on.
        response.TryGetOriginalException(out var cause);
        throw new InvalidOperationException($"Elasticsearch could not {operation}: {response.DebugInformation}", cause);
    }

    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        var response = await _client.Indices
            .CreateAsync(ListingIndex.CreateRequest(_index), cancellationToken)
            .ConfigureAwait(false);

        EnsureSuccess(response, $"create index '{_indexName}'");
        LogCreated(_indexName);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created Elasticsearch index {Index}.")]
    private partial void LogCreated(string index);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted Elasticsearch index {Index}.")]
    private partial void LogDeleted(string index);
}
