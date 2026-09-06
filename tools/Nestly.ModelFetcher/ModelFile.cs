using System.ComponentModel.DataAnnotations;

namespace Nestly.ModelFetcher;

/// <summary>One file to download and the digest that proves it arrived intact.</summary>
internal sealed class ModelFile
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public Uri? Url { get; init; }

    /// <summary>Lowercase hex SHA-256. A mismatch is a hard failure, never a warning.</summary>
    [Required]
    [RegularExpression("^[0-9a-f]{64}$")]
    public string Sha256 { get; init; } = string.Empty;
}
