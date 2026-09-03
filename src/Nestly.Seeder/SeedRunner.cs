using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Seeder.Cleaning;
using Nestly.Seeder.Csv;

namespace Nestly.Seeder;

/// <summary>Reads the dataset, cleans every row, and reports what survived.</summary>
internal sealed partial class SeedRunner
{
    private readonly ILogger<SeedRunner> _logger;
    private readonly SeederOptions _options;

    public SeedRunner(ILogger<SeedRunner> logger, IOptions<SeederOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _options = options.Value;
    }

    public int Run()
    {
        var path = ResolvePath(_options.DataPath);

        if (!File.Exists(path))
        {
            LogDatasetMissing(path);
            return 1;
        }

        var source = new ListingCsvSource();
        var stopwatch = Stopwatch.StartNew();
        var listings = 0;
        var amenities = 0;

        foreach (var listing in source.Stream(path, _options.Limit))
        {
            listings++;
            amenities += listing.Amenities.Count;
        }

        stopwatch.Stop();

        var perListing = listings == 0 ? 0d : Math.Round((double)amenities / listings, 1);
        LogParsed(listings, source.Read, stopwatch.ElapsedMilliseconds, perListing);

        foreach (var (reason, count) in source.Skipped.OrderByDescending(entry => entry.Value))
        {
            LogDropped(count, reason);
        }

        return listings > 0 ? 0 : 1;
    }

    /// <summary>
    /// Resolves a configured path against the repository root when it is relative, so one
    /// setting works from a shell, from an IDE with a working directory of its own choosing, and
    /// from a container image where the dataset was copied in beside the binary.
    /// </summary>
    private static string ResolvePath(string configured)
    {
        if (Path.IsPathRooted(configured))
        {
            return configured;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Nestly.slnx")))
            {
                return Path.Combine(directory.FullName, configured);
            }

            directory = directory.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, configured);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Dataset not found at {Path}. Set Seeder:DataPath or pass --file.")]
    private partial void LogDatasetMissing(string path);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Parsed {Listings} listings from {Rows} rows in {ElapsedMs} ms, {AmenitiesPerListing} amenities each on average.")]
    private partial void LogParsed(int listings, int rows, long elapsedMs, double amenitiesPerListing);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dropped {Count} rows: {Reason}.")]
    private partial void LogDropped(int count, ListingSkipReason reason);
}
