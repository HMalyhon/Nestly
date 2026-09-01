using System.Globalization;
using System.IO.Compression;
using System.Text;
using CsvHelper;

namespace Nestly.DataTrimmer;

/// <summary>
/// Derives the committed <c>data/listings.csv.gz</c> from the upstream Inside Airbnb snapshot.
/// </summary>
/// <remarks>
/// Every step is a projection, a drop or a sample -- no value is invented, rewritten or
/// reformatted, so the committed subset is real data or it is nothing.
/// <para>
/// The output is reproducible: the sample is seeded, rows are sorted by id, and the archive
/// carries no timestamp. That matters because the result is committed; a run-to-run difference
/// would show up as a spurious diff on a 1.2 MB binary every time anyone regenerated it.
/// </para>
/// </remarks>
internal static class DatasetTrimmer
{
    /// <summary>
    /// The 17 columns Nestly indexes, in output order. Upstream has 90; the rest are either
    /// unused -- availability windows, revenue estimates, per-category review scores -- or host
    /// personal data this project has no business redistributing.
    /// </summary>
    public static readonly string[] Columns =
    [
        "id",
        "name",
        "description",
        "neighbourhood_cleansed",
        "neighbourhood_group_cleansed",
        "latitude",
        "longitude",
        "property_type",
        "room_type",
        "accommodates",
        "bathrooms_text",
        "bedrooms",
        "amenities",
        "price",
        "minimum_nights",
        "review_scores_rating",
        "last_review",
    ];

    /// <summary>
    /// A row missing any of these cannot be indexed as a searchable listing, so it is dropped
    /// rather than back-filled. <c>bedrooms</c> is the expensive one -- 41% of upstream rows have
    /// no value -- but mapping those to 0 would invent a studio population the data does not have.
    /// </summary>
    private static readonly string[] Required =
    [
        "name",
        "description",
        "latitude",
        "longitude",
        "bedrooms",
        "bathrooms_text",
        "amenities",
        "price",
        "neighbourhood_cleansed",
    ];

    public static TrimReport Trim(DatasetOptions options, string rawPath, string outputPath)
    {
        var rows = Read(options, rawPath, out var read, out var complete);

        if (rows.Count < options.SampleSize)
        {
            throw new InvalidOperationException($"only {rows.Count} eligible rows, need {options.SampleSize}");
        }

        var sample = Sample(options, rows);
        Write(outputPath, sample);

        return new TrimReport(read, complete, rows.Count, sample.Count, new FileInfo(outputPath).Length);
    }

    /// <summary>Parses an upstream price literal such as <c>"$1,250.00"</c>.</summary>
    private static bool TryParsePrice(string? raw, out decimal price)
    {
        price = 0m;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var cleaned = raw.Trim().TrimStart('$').Replace(",", string.Empty, StringComparison.Ordinal);
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out price);
    }

    private static List<(long Id, string[] Values)> Read(
        DatasetOptions options,
        string rawPath,
        out int read,
        out int complete)
    {
        read = 0;
        complete = 0;
        var eligible = new List<(long Id, string[] Values)>();

        using var file = File.OpenRead(rawPath);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var text = new StreamReader(gzip, Encoding.UTF8);
        using var csv = new CsvReader(text, CultureInfo.InvariantCulture);

        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            read++;

            if (Array.Exists(Required, column => string.IsNullOrWhiteSpace(csv.GetField(column))))
            {
                continue;
            }

            complete++;

            if (!TryParsePrice(csv.GetField("price"), out var price) ||
                price < options.MinPricePerNight ||
                price > options.MaxPricePerNight)
            {
                continue;
            }

            var values = Array.ConvertAll(Columns, column => csv.GetField(column) ?? string.Empty);
            eligible.Add((long.Parse(values[0], CultureInfo.InvariantCulture), values));
        }

        return eligible;
    }

    private static List<(long Id, string[] Values)> Sample(
        DatasetOptions options,
        List<(long Id, string[] Values)> eligible)
    {
        // A partial Fisher-Yates: swap a random survivor into each of the first SampleSize slots.
        // Uniform, one pass, and seeded, so the same rows are chosen on every machine.
#pragma warning disable S2245 // Reproducibility is the requirement here; a cryptographic RNG cannot be seeded.
        var random = new Random(options.Seed);
#pragma warning restore S2245

        for (var i = 0; i < options.SampleSize; i++)
        {
            var j = random.Next(i, eligible.Count);
            (eligible[i], eligible[j]) = (eligible[j], eligible[i]);
        }

        var sample = eligible.GetRange(0, options.SampleSize);

        // The sample is uniform rather than stratified, so the borough distribution stays true:
        // Manhattan and Brooklyn dominant, Staten Island sparse. Sorting by id afterwards gives
        // the committed file a stable order that is not the sampler's.
        sample.Sort((left, right) => left.Id.CompareTo(right.Id));

        return sample;
    }

    private static void Write(string outPath, List<(long Id, string[] Values)> rows)
    {
        using var file = File.Create(outPath);
        using var gzip = new GZipStream(file, CompressionLevel.SmallestSize);
        using var text = new StreamWriter(gzip, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        using var csv = new CsvWriter(text, CultureInfo.InvariantCulture);

        foreach (var column in Columns)
        {
            csv.WriteField(column);
        }

        csv.NextRecord();

        foreach (var row in rows)
        {
            foreach (var value in row.Values)
            {
                csv.WriteField(value);
            }

            csv.NextRecord();
        }
    }
}
