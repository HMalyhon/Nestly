using DotNet.Testcontainers.Builders;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nestly.Domain;
using Nestly.Search;
using Nestly.Search.Embedding;
using Nestly.Search.Indexing;
using Nestly.Seeder.Csv;
using Nestly.Seeder.Embedding;
using Testcontainers.Elasticsearch;

namespace Nestly.IntegrationTests;

/// <summary>
/// One Elasticsearch 9.2.0 container for the whole run, holding a fixed cut of the real dataset.
/// </summary>
/// <remarks>
/// A real cluster rather than a stub, because everything worth testing here is Elasticsearch's
/// own behaviour: whether a terms aggregation excludes its own filter, whether a kNN filter
/// actually applies, whether paging is stable. A stub would only prove the stub agrees with
/// itself. Shared across every test class, since a container per class would spend more time
/// starting Java than searching.
/// </remarks>
public sealed class ElasticsearchFixture : IAsyncLifetime
{
    /// <summary>Rows taken from the top of the dataset: fixed, so a failure is reproducible.</summary>
    // A thousand, not the couple of hundred a fixture usually needs, because the search fuses the
    // top 100 of each leg: against 200 documents each leg retrieves half the corpus, the two sets
    // overlap almost entirely, and every hit comes back marked Both. The hybrid layer only has an
    // observable effect when the retrieval depth is a fraction of the index, as it is in the real
    // one, so the fixture has to be big enough for the legs to disagree.
    public const int ListingCount = 1_000;

    /// <summary>Descriptions per inference call, and documents per bulk request.</summary>
    private const int EmbeddingBatchSize = 32;
    private const int IndexBatchSize = 200;

    private readonly ElasticsearchContainer _container =
        new ElasticsearchBuilder("docker.elastic.co/elasticsearch/elasticsearch:9.2.0")
        .WithEnvironment("discovery.type", "single-node")
        .WithEnvironment("xpack.security.enabled", "false")
        .WithEnvironment("xpack.license.self_generated.type", "basic")
        .WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m")

        // The module's own wait strategy is built for a secured cluster; this one only cares that
        // the node answers, which is all a plaintext single node has to do.
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilHttpRequestIsSucceeded(request => request.ForPort(9200).ForPath("/_cluster/health")))
        .Build();

    private ServiceProvider? _services;

    /// <summary>The listings that were indexed, so a test can assert against the same data.</summary>
    public IReadOnlyList<Listing> Seeded { get; private set; } = [];

    /// <summary>True when the ONNX model was on disk and the documents carry real vectors.</summary>
    public bool HasVectors { get; private set; }

    public IServiceProvider Services => _services
        ?? throw new InvalidOperationException("The fixture has not been initialised.");

    public ElasticsearchClient Client => Services.GetRequiredService<ElasticsearchClient>();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);

        var root = RepositoryRoot();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Built here rather than taken from GetConnectionString(), which returns an
                // https URL with credentials for the secured cluster the module assumes. Security
                // is off above, matching the Compose stack, so the client must speak plaintext or
                // it hangs on a TLS handshake the server never answers.
                ["Elasticsearch:Uri"] = $"http://{_container.Hostname}:{_container.GetMappedPublicPort(9200)}",
                ["Elasticsearch:IndexName"] = "listings-test",
                ["Embedding:ModelDirectory"] = Path.Combine(root, "models"),
                ["Embedding:ModelFile"] = "all-MiniLM-L6-v2.onnx",
                ["Embedding:VocabFile"] = "vocab.txt",
            })
            .Build();

        _services = new ServiceCollection()
            .AddLogging()
            .AddNestlySearch(configuration)
            .BuildServiceProvider();

        await Services.GetRequiredService<IListingIndexProvisioner>()
            .RecreateAsync()
            .ConfigureAwait(false);

        Seeded = [.. new ListingCsvSource().Stream(Path.Combine(root, "data", "listings.csv.gz"), ListingCount)];

        await IndexAsync(Vectorize(Seeded)).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync().ConfigureAwait(false);
        }

        await _container.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Walks up from the test binaries to the directory holding the solution.</summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Nestly.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not find the repository root from the test binaries.");
    }

    /// <summary>Embeds the fixture if the model is present, and says so if it is not.</summary>
    // Through the seeder's own vectorizer, which embeds a batch at a time. Handing the model all
    // thousand descriptions at once asks it for a 1000 x 256 x 384 output tensor -- 393 MB before
    // the activations behind it -- and the test host is killed before Elasticsearch sees a
    // document. The model is also 90 MB and deliberately not committed, so a clone without it
    // still runs every test that does not need a vector.
    private IEnumerable<Listing> Vectorize(IReadOnlyList<Listing> listings)
    {
        IListingEmbedder embedder;

        try
        {
            embedder = Services.GetRequiredService<IListingEmbedder>();
        }
        catch (Exception cause) when (cause is FileNotFoundException or DirectoryNotFoundException)
        {
            return listings;
        }

        HasVectors = true;

        return new ListingVectorizer(embedder, EmbeddingBatchSize).Apply(listings);
    }

    private async Task IndexAsync(IEnumerable<Listing> listings)
    {
        // Chunked, so the vectorizer above stays lazy: one bulk request carrying a thousand
        // 384-float vectors would pull every batch through the model before anything is sent.
        foreach (var chunk in listings.Chunk(IndexBatchSize))
        {
            var response = await Client
                .BulkAsync(bulk => bulk
                    .Index("listings-test")
                    .IndexMany(chunk, (descriptor, listing) => descriptor.Id(listing.Id)))
                .ConfigureAwait(false);

            if (!response.IsValidResponse || response.Errors)
            {
                throw new InvalidOperationException($"Seeding the fixture failed: {response.DebugInformation}");
            }
        }

        // Refresh rather than wait out the second: the index is only useful to a test once the
        // documents are searchable, and every assertion below would otherwise race it.
        await Client.Indices.RefreshAsync(refresh => refresh.Indices("listings-test")).ConfigureAwait(false);
    }
}
