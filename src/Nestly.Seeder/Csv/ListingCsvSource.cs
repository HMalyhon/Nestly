using System.Globalization;
using System.IO.Compression;
using System.Text;
using CsvHelper;
using Nestly.Domain;
using Nestly.Seeder.Cleaning;

namespace Nestly.Seeder.Csv;

/// <summary>Streams listings out of the gzipped dataset, cleaning as it goes.</summary>
/// <remarks>
/// Nothing is unpacked to disk and nothing is buffered whole: the file is decompressed, parsed
/// and mapped one row at a time. 5,000 rows would fit in memory comfortably, but a seeder that
/// only works on a small file is a seeder with a cliff in it.
/// </remarks>
internal sealed class ListingCsvSource
{
    private readonly Dictionary<ListingSkipReason, int> _skipped = [];

    /// <summary>How many rows were dropped, by reason. Populated as the stream is consumed.</summary>
    public IReadOnlyDictionary<ListingSkipReason, int> Skipped => _skipped;

    public int Read { get; private set; }

    public IEnumerable<Listing> Stream(string path, int limit = 0)
    {
        _skipped.Clear();
        Read = 0;

        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var text = new StreamReader(gzip, Encoding.UTF8);
        using var csv = new CsvReader(text, CultureInfo.InvariantCulture);

        var yielded = 0;

        foreach (var row in csv.GetRecords<ListingCsvRow>())
        {
            Read++;

            if (!ListingMapper.TryMap(row, out var listing, out var reason))
            {
                _skipped[reason] = _skipped.GetValueOrDefault(reason) + 1;
                continue;
            }

            yield return listing;

            if (limit > 0 && ++yielded >= limit)
            {
                yield break;
            }
        }
    }
}
