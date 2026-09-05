using System.Text.Json;
using System.Text.Json.Serialization;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Serialization;
using Elastic.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nestly.Search.Configuration;
using Nestly.Search.Indexing;
using Nestly.Search.Searching;

namespace Nestly.Search;

public static class SearchServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Elasticsearch client and everything in this assembly that depends on it.
    /// </summary>
    public static IServiceCollection AddNestlySearch(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ElasticsearchOptions>()
            .Bind(configuration.GetSection(ElasticsearchOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // The client is thread-safe, pools connections and is expensive to build, so it is a
        // singleton -- one per process, as Elastic's own guidance has it.
        services.AddSingleton(provider =>
            CreateClient(provider.GetRequiredService<IOptions<ElasticsearchOptions>>().Value));

        services.AddSingleton<IListingIndexProvisioner, ListingIndexProvisioner>();
        services.AddSingleton<IListingSearchService, ListingSearchService>();
        services.AddSingleton<IListingMapService, ListingMapService>();
        services.AddSingleton<IListingDetailService, ListingDetailService>();
        services.AddSingleton<IListingSuggestService, ListingSuggestService>();

        return services;
    }

    private static ElasticsearchClient CreateClient(ElasticsearchOptions options)
    {
        var settings = new ElasticsearchClientSettings(
                new SingleNodePool(options.Uri),
                sourceSerializer: (_, clientSettings) => new DefaultSourceSerializer(clientSettings, ConfigureJson))
            .DefaultIndex(options.IndexName)
            .RequestTimeout(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            settings = settings.Authentication(new ApiKey(options.ApiKey));
        }

        if (options.EnableDebugMode)
        {
            settings = settings.EnableDebugMode();
        }

        return new ElasticsearchClient(settings);
    }

    private static void ConfigureJson(JsonSerializerOptions json)
    {
        // Nulls are dropped rather than written. A listing with no reviews should have no
        // reviewScore field at all -- indexing an explicit null costs bytes in _source and,
        // for descriptionVector, would send 4 bytes to say "no vector" on every unembedded doc.
        json.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    }
}
