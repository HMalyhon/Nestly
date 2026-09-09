using System.Text.Json;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport.Extensions;

namespace Nestly.UnitTests.Querying;

/// <summary>Serialises a query the way the client would put it on the wire.</summary>
// Asserting on the emitted JSON rather than on the union type's properties: the JSON is the
// contract Elasticsearch actually reads, and it stays readable when the client's object model
// changes underneath. The client is never connected; only its serialiser is used.
internal static class Dsl
{
    private static readonly ElasticsearchClient Client =
        new(new ElasticsearchClientSettings(new Uri("http://localhost:9200")));

    public static JsonElement Of<T>(T value)
    {
        using var document = JsonDocument.Parse(Client.RequestResponseSerializer.SerializeToString(value));

        return document.RootElement.Clone();
    }

    public static string Text<T>(T value) => Client.RequestResponseSerializer.SerializeToString(value);

    /// <summary>The clauses under a named property, however the serialiser chose to shape them.</summary>
    // Elasticsearch accepts a lone clause either bare or wrapped in an array, and the client
    // emits whichever it was handed, so a test that assumes an array breaks on a single filter.
    public static IReadOnlyList<JsonElement> Clauses(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            return [];
        }

        return value.ValueKind == JsonValueKind.Array ? [.. value.EnumerateArray()] : [value];
    }
}
