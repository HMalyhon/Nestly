using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Nestly.DataTrimmer;

/// <summary>
/// Regenerates the committed dataset:
/// <code>dotnet run --project tools/Nestly.DataTrimmer</code>
/// </summary>
/// <remarks>
/// This is the answer to "where did data/listings.csv.gz come from?". It is a tool rather than a
/// paragraph in the README because a description of a transformation cannot be re-run, and a
/// committed data file whose derivation nobody can reproduce is just a binary blob to trust.
/// </remarks>
internal static class Program
{
    private static int Main(string[] args)
    {
        var overrides = new Dictionary<string, string?>(StringComparer.Ordinal);

        if (!TryParseArguments(args, overrides, out var exitCode))
        {
            return exitCode;
        }

        var options = Load(overrides);

        try
        {
            Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);
        }
        catch (ValidationException invalid)
        {
            Console.Error.WriteLine($"appsettings.json is not usable: {invalid.Message}");
            return 2;
        }

        // Paths are stored repository-relative so the settings file reads the same on every
        // machine; they are resolved against the repository root, not the working directory,
        // so the tool behaves identically however it was launched.
        var root = FindRepositoryRoot();
        var raw = Path.GetFullPath(Path.Combine(root, options.RawPath));
        var output = Path.GetFullPath(Path.Combine(root, options.OutputPath));

        if (!File.Exists(raw))
        {
            PrintMissingRaw(raw, options.SnapshotUrl!);
            return 1;
        }

        var report = DatasetTrimmer.Trim(options, raw, output);

        Console.WriteLine(Format("read", report.Read, $"rows from {raw}"));
        Console.WriteLine(Format("complete", report.Complete, $"rows ({report.Read - report.Complete} dropped, missing a required field)"));
        Console.WriteLine(Format(
            "in range",
            report.Eligible,
            $"rows ({report.Complete - report.Eligible} dropped, outside ${options.MinPricePerNight}-${options.MaxPricePerNight}/night)"));
        Console.WriteLine(Format("wrote", report.Written, $"rows to {output} ({report.Bytes / 1_000_000d:F1} MB gzipped)"));

        return 0;
    }

    private static DatasetOptions Load(Dictionary<string, string?> overrides)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddInMemoryCollection(overrides)
            .Build();

        return configuration.GetSection(DatasetOptions.SectionName).Get<DatasetOptions>() ?? new DatasetOptions();
    }

    private static bool TryParseArguments(string[] args, Dictionary<string, string?> overrides, out int exitCode)
    {
        exitCode = 0;
        var index = 0;

        while (index < args.Length)
        {
            var argument = args[index++];

            switch (argument)
            {
                case "--raw" when index < args.Length:
                    overrides[$"{DatasetOptions.SectionName}:{nameof(DatasetOptions.RawPath)}"] = args[index++];
                    break;
                case "--out" when index < args.Length:
                    overrides[$"{DatasetOptions.SectionName}:{nameof(DatasetOptions.OutputPath)}"] = args[index++];
                    break;
                case "--help" or "-h":
                    PrintUsage();
                    return false;
                default:
                    Console.Error.WriteLine($"unrecognised argument: {argument}");
                    PrintUsage();
                    exitCode = 2;
                    return false;
            }
        }

        return true;
    }

    private static string Format(string label, int count, string detail) =>
        string.Create(CultureInfo.InvariantCulture, $"{label,-10} {count,6} {detail}");

    /// <summary>
    /// Walks up from the binary to the directory holding the solution, so the tool works the same
    /// whether it is launched from the repository root, from its own project folder, or by an IDE
    /// with a working directory of its own choosing.
    /// </summary>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Nestly.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("usage: dotnet run --project tools/Nestly.DataTrimmer [--raw <path>] [--out <path>]");
        Console.Error.WriteLine("everything else -- snapshot URL, sample size, seed, price range -- lives in appsettings.json");
    }

    private static void PrintMissingRaw(string raw, Uri snapshotUrl)
    {
        Console.Error.WriteLine($"missing {raw}");
        Console.Error.WriteLine("The upstream file is gitignored: it is 15 MB and only the trimmed subset is committed.");
        Console.Error.WriteLine("Fetch the snapshot this subset was built from:");
        Console.Error.WriteLine();
        Console.Error.WriteLine($"  mkdir -p {Path.GetDirectoryName(raw)}");
        Console.Error.WriteLine($"  curl -L -o {raw} \\");
        Console.Error.WriteLine($"    {snapshotUrl}");
        Console.Error.WriteLine();
        Console.Error.WriteLine("If that URL has rotated, see data/README.md.");
    }
}
