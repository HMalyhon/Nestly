using System.ComponentModel.DataAnnotations;

namespace Nestly.Search.Configuration;

/// <summary>
/// Connection settings for the Elasticsearch cluster, bound from the <c>Elasticsearch</c>
/// configuration section and validated at startup.
/// </summary>
/// <remarks>
/// Validation runs on start rather than on first use, so a typo in configuration fails the
/// process immediately instead of surfacing as a failed search request minutes later.
/// </remarks>
public sealed class ElasticsearchOptions
{
    public const string SectionName = "Elasticsearch";

    /// <summary>
    /// Cluster endpoint, supplied by whichever host app uses this assembly -- its appsettings in
    /// development, an environment variable in Compose. There is deliberately no default: a
    /// fallback to localhost would let a misconfigured deployment start up and then fail one
    /// search at a time, where a missing value fails the process on the spot.
    /// </summary>
    [Required]
    public Uri Uri { get; init; } = null!;

    /// <summary>
    /// Index the API reads and the seeder writes. Configurable so integration tests can point at
    /// a throwaway index without colliding with a running demo.
    /// </summary>
    [Required]
    [RegularExpression("^[a-z0-9][a-z0-9_.-]*$", ErrorMessage = "Index names must be lowercase and may not start with _, - or +.")]
    public string IndexName { get; init; } = "listings";

    /// <summary>
    /// Optional API key. The local Compose cluster runs with security disabled and needs none;
    /// this exists so the same build can talk to a secured cluster without a code change.
    /// </summary>
    public string? ApiKey { get; init; }

    [Range(1, 300)]
    public int RequestTimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Captures request and response bodies on the client and logs them. Costs an extra buffer
    /// copy per request, so it stays off unless something needs explaining.
    /// </summary>
    public bool EnableDebugMode { get; init; }
}
