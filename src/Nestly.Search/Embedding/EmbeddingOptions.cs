using System.ComponentModel.DataAnnotations;

namespace Nestly.Search.Embedding;

/// <summary>Where the sentence-embedding model lives and how much of a description it reads.</summary>
public sealed class EmbeddingOptions
{
    public const string SectionName = "Embedding";

    /// <summary>
    /// Directory holding the ONNX model and its vocabulary, relative to the repository root when
    /// there is one and to the application directory otherwise -- which is the Docker case.
    /// </summary>
    [Required]
    public string ModelDirectory { get; init; } = string.Empty;

    [Required]
    public string ModelFile { get; init; } = string.Empty;

    [Required]
    public string VocabFile { get; init; } = string.Empty;

    /// <summary>
    /// Tokens kept from a description. all-MiniLM-L6-v2 was trained at 256 and its position
    /// embeddings stop at 512; the listings that exceed 256 lose only their closing pleasantries.
    /// </summary>
    [Range(16, 512)]
    public int MaxTokens { get; init; } = 256;

    /// <summary>Descriptions embedded per inference call.</summary>
    [Range(1, 512)]
    public int BatchSize { get; init; } = 32;
}
