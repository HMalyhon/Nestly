namespace Nestly.Api.Infrastructure;

/// <summary>Origins allowed to call the API from a browser.</summary>
// Configured because the front end is on Vite's dev server locally and behind the same nginx in
// Compose, where it needs no CORS at all.
internal sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public const string PolicyName = "nestly-web";

    public IReadOnlyList<string> AllowedOrigins { get; init; } = [];
}
