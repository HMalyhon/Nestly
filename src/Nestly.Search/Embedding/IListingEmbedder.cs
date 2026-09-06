namespace Nestly.Search.Embedding;

/// <summary>Turns text into a unit vector in the same space the index was built with.</summary>
// One interface for both sides of the system on purpose: the seeder embeds descriptions and the
// search embeds queries, and a hybrid search only works if they agree to the last decimal.
public interface IListingEmbedder
{
    /// <summary>Length of every vector this embedder produces.</summary>
    int Dimensions { get; }

    /// <summary>
    /// Embeds one string. Empty input still returns a unit vector -- the one BERT produces from a
    /// bare [CLS][SEP] -- so callers that mean "no vector" must not embed and check.
    /// </summary>
    float[] Embed(string text);

    /// <summary>Embeds a batch in one inference call, which is several times faster per item.</summary>
    IReadOnlyList<float[]> Embed(IReadOnlyList<string> texts);
}
