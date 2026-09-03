using System.ComponentModel.DataAnnotations;

namespace Nestly.Seeder;

internal sealed class SeederOptions
{
    public const string SectionName = "Seeder";

    /// <summary>
    /// The dataset to read. Relative paths resolve against the repository root, so the same
    /// settings work from a developer's shell and from a container where the file was copied in.
    /// </summary>
    [Required]
    public string DataPath { get; init; } = string.Empty;

    /// <summary>Stop after this many listings. Zero, the default, means the whole file.</summary>
    [Range(0, int.MaxValue)]
    public int Limit { get; init; }

    /// <summary>
    /// Documents per bulk request. Large enough that the round trips disappear, small enough
    /// that a failed batch is cheap to retry and the demo cluster's 1 GB heap is never asked to
    /// hold much at once.
    /// </summary>
    [Range(1, 10_000)]
    public int BatchSize { get; init; } = 500;

    /// <summary>Parse and report without touching Elasticsearch. Useful when only the cleaning is in question.</summary>
    public bool DryRun { get; init; }
}
