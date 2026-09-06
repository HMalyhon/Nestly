using System.ComponentModel.DataAnnotations;

namespace Nestly.ModelFetcher;

/// <summary>
/// Which model files to fetch and what they must hash to, bound from <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// The URLs live here rather than in code for the same reason the dataset snapshot URL does: where
/// a file came from is a fact about the file, and a downloader has no business knowing where the
/// internet is. The digests make it a verified fetch instead of a hopeful one.
/// </remarks>
internal sealed class ModelOptions
{
    public const string SectionName = "Model";

    /// <summary>Repository-relative directory the files are written to. Gitignored: 90 MB.</summary>
    [Required]
    public string Directory { get; init; } = string.Empty;

    [Required]
    [MinLength(1)]
    public IReadOnlyList<ModelFile> Files { get; init; } = [];
}
