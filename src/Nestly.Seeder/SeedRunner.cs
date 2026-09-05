using System.Diagnostics;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Domain;
using Nestly.Search.Configuration;
using Nestly.Search.Indexing;
using Nestly.Seeder.Cleaning;
using Nestly.Seeder.Csv;

namespace Nestly.Seeder;

/// <summary>Recreates the index and fills it with the cleaned dataset.</summary>
/// <remarks>
/// The run drops and recreates the index rather than upserting into whatever was there before.
/// Re-seeding is then idempotent by construction: the result depends on the dataset and the
/// current mapping, never on how many times the seeder has been run or on which mapping was in
/// force the last time.
/// </remarks>
internal sealed partial class SeedRunner
{
    private readonly ElasticsearchClient _client;
    private readonly IListingIndexProvisioner _provisioner;
    private readonly ILogger<SeedRunner> _logger;
    private readonly SeederOptions _options;
    private readonly string _indexName;
    private readonly Uri _clusterUri;

    public SeedRunner(
        ElasticsearchClient client,
        IListingIndexProvisioner provisioner,
        IOptions<SeederOptions> options,
        IOptions<ElasticsearchOptions> elasticsearch,
        ILogger<SeedRunner> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(elasticsearch);

        _client = client;
        _provisioner = provisioner;
        _logger = logger;
        _options = options.Value;
        _indexName = elasticsearch.Value.IndexName;
        _clusterUri = elasticsearch.Value.Uri;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var path = ResolvePath(_options.DataPath);

        if (!File.Exists(path))
        {
            LogDatasetMissing(path);
            return 1;
        }

        if (!_options.DryRun && !await IsClusterReachableAsync(cancellationToken).ConfigureAwait(false))
        {
            return 1;
        }

        var source = new ListingCsvSource();
        var listings = source.Stream(path, _options.Limit);
        var stopwatch = Stopwatch.StartNew();

        int indexed;

        try
        {
            indexed = _options.DryRun
                ? Parse(listings)
                : await IndexAsync(listings, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            // Broad on purpose: this is the process boundary, and BulkAll can surface anything
            // from a rejected document to a transport error. Either way the operator needs the
            // reason and a non-zero exit, not a stack trace.
            LogSeedFailed(failure);
            return 1;
        }

        stopwatch.Stop();

        ReportSkipped(source);

        if (indexed == 0)
        {
            LogNothingIndexed(path);
            return 1;
        }

        var perSecond = (int)(indexed / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001));

        if (_options.DryRun)
        {
            LogParsed(indexed, source.Read, stopwatch.ElapsedMilliseconds);
            return 0;
        }

        LogIndexed(indexed, source.Read, stopwatch.ElapsedMilliseconds, perSecond);

        // Ask the cluster what it actually holds rather than trusting the count kept while
        // sending. A bulk item can be rejected on the server for a reason the client shrugs at,
        // and "the seeder said 5,000" is exactly the claim worth checking.
        var count = await _client.CountAsync<Listing>(search => search.Indices(_indexName), cancellationToken)
            .ConfigureAwait(false);

        if (!count.IsValidResponse)
        {
            LogCountFailed(count.DebugInformation);
            return 1;
        }

        LogClusterCount(count.Count, _indexName);

        return count.Count == indexed ? 0 : 1;
    }

    /// <summary>
    /// Resolves a configured path against the repository root when it is relative, so one
    /// setting works from a shell, from an IDE with a working directory of its own choosing, and
    /// from a container image where the dataset was copied in beside the binary.
    /// </summary>
    private static string ResolvePath(string configured)
    {
        if (Path.IsPathRooted(configured))
        {
            return configured;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Nestly.slnx")))
            {
                return Path.Combine(directory.FullName, configured);
            }

            directory = directory.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, configured);
    }

    /// <summary>Enumerating the stream is the dry run: every row is parsed and cleaned, nothing is sent.</summary>
    private static int Parse(IEnumerable<Listing> listings) => listings.Count();

    /// <summary>
    /// One ping before reading 5,000 rows. A cluster that is down is the likeliest reason for
    /// this tool to fail, and the difference between a sentence naming the address and a
    /// transport stack trace is the difference between a fixable problem and a puzzling one.
    /// </summary>
    private async Task<bool> IsClusterReachableAsync(CancellationToken cancellationToken)
    {
        var ping = await _client.PingAsync(cancellationToken).ConfigureAwait(false);

        if (ping.IsValidResponse)
        {
            return true;
        }

        var reason = ping.DebugInformation.Split('\n', 2)[0];
        LogClusterUnreachable(_clusterUri, reason);

        return false;
    }

    private async Task<int> IndexAsync(IEnumerable<Listing> listings, CancellationToken cancellationToken)
    {
        LogRecreating(_indexName);
        await _provisioner.RecreateAsync(cancellationToken).ConfigureAwait(false);

        var indexed = 0;

        // BulkAll handles the batching, the back-off and the retries; what it does not do is
        // finish, so the observable is bridged to a Task the run can await and cancel.
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var observable = _client.BulkAll(
            listings,
            bulk => bulk
                .Index(_indexName)
                .Size(_options.BatchSize)

                // The demo cluster is one node with a 1 GB heap. Four parallel bulk requests
                // would be a reasonable default against a real cluster and a way to provoke
                // rejections against this one.
                .MaxDegreeOfParallelism(2)
                .BackOffTime(TimeSpan.FromSeconds(5))
                .BackOffRetries(2)

                // Refresh once at the end. Without it the index is correct but invisible for a
                // second, which reads as "the seeder ran and search returns nothing".
                .RefreshOnCompleted(true)

                // A document the cluster refuses -- a mapping conflict, say -- ends the run.
                // Seeding 4,999 of 5,000 documents and exiting 0 is how a broken index reaches
                // a demo.
                .ContinueAfterDroppedDocuments(false)
                .DroppedDocumentCallback((item, listing) => LogDropped(listing.Id, item.Error?.Reason ?? item.Result)),
            cancellationToken);

        using var subscription = observable.Subscribe(new BulkAllObserver(
            onNext: response =>
            {
                // Interlocked, not +=, because MaxDegreeOfParallelism means this callback runs
                // on two threads at once: a plain increment loses a whole page to the race, and
                // the seeder then under-reports what it actually indexed.
                var total = Interlocked.Add(ref indexed, response.Items.Count);
                LogProgress(total);
            },
            onError: error => completion.TrySetException(error),
            onCompleted: () => completion.TrySetResult()));

        await completion.Task.ConfigureAwait(false);

        return indexed;
    }

    private void ReportSkipped(ListingCsvSource source)
    {
        foreach (var (reason, count) in source.Skipped.OrderByDescending(entry => entry.Value))
        {
            LogSkipped(count, reason);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Seeding failed.")]
    private partial void LogSeedFailed(Exception failure);

    [LoggerMessage(Level = LogLevel.Error, Message = "Dataset not found at {Path}. Set Seeder:DataPath or pass --file.")]
    private partial void LogDatasetMissing(string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "No listings came out of {Path}; nothing was indexed.")]
    private partial void LogNothingIndexed(string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recreating index {Index}.")]
    private partial void LogRecreating(string index);

    [LoggerMessage(Level = LogLevel.Information, Message = "Indexed {Indexed} listings so far.")]
    private partial void LogProgress(int indexed);

    [LoggerMessage(Level = LogLevel.Information, Message = "Parsed {Listings} listings from {Rows} rows in {ElapsedMs} ms. Dry run: nothing was indexed.")]
    private partial void LogParsed(int listings, int rows, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Indexed {Indexed} listings from {Rows} rows in {ElapsedMs} ms ({PerSecond}/s).")]
    private partial void LogIndexed(int indexed, int rows, long elapsedMs, int perSecond);

    [LoggerMessage(Level = LogLevel.Information, Message = "Index {Index} now holds {Count} documents.")]
    private partial void LogClusterCount(long count, string index);

    [LoggerMessage(Level = LogLevel.Error, Message = "Elasticsearch at {Uri} is not reachable: {Reason}")]
    private partial void LogClusterUnreachable(Uri uri, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not count documents after seeding: {Details}")]
    private partial void LogCountFailed(string details);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipped {Count} rows: {Reason}.")]
    private partial void LogSkipped(int count, ListingSkipReason reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Elasticsearch rejected listing {Id}: {Reason}")]
    private partial void LogDropped(string id, string? reason);
}
