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
}
