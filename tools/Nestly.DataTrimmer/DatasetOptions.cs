using System.ComponentModel.DataAnnotations;

namespace Nestly.DataTrimmer;

/// <summary>
/// Everything that decides what the committed subset contains, bound from
/// <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// These are inputs, not logic: which snapshot the data came from, which rows survive, how many
/// are kept. Holding them in settings puts the whole derivation on one readable screen -- the
/// numbers data/README.md quotes can be checked against this file rather than read out of the
/// middle of a method -- and keeps the provenance URL a fact about the data instead of a string
/// literal in a class that has no business knowing where the internet is.
/// </remarks>
internal sealed class DatasetOptions
{
    public const string SectionName = "Dataset";

    /// <summary>
    /// The exact upstream file the subset was cut from. Inside Airbnb publishes quarterly and
    /// date-stamps its URLs, so this one will eventually stop resolving -- which is why the
    /// subset is committed rather than downloaded.
    /// </summary>
    [Required]
    public Uri? SnapshotUrl { get; init; }

    /// <summary>Repository-relative path to the upstream snapshot. Gitignored: 15 MB.</summary>
    [Required]
    public string RawPath { get; init; } = string.Empty;

    /// <summary>Repository-relative path to the trimmed subset. Committed.</summary>
    [Required]
    public string OutputPath { get; init; } = string.Empty;

    [Range(1, 1_000_000)]
    public int SampleSize { get; init; }

    /// <summary>Seeded from the snapshot date, so the choice of rows is reproducible and not a matter of taste.</summary>
    public int Seed { get; init; }

    [Range(0, 100_000)]
    public int MinPricePerNight { get; init; }

    /// <summary>
    /// The upstream tail reaches $31,211/night, which would stretch the price facet far enough to
    /// make the slider useless for the 99% of listings below the ceiling.
    /// </summary>
    [Range(1, 100_000)]
    public int MaxPricePerNight { get; init; }
}
