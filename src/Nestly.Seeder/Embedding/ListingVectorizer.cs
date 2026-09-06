using System.Diagnostics;
using Nestly.Domain;
using Nestly.Search.Embedding;

namespace Nestly.Seeder.Embedding;

/// <summary>
/// Attaches a description vector to each listing on its way to the bulk indexer.
/// </summary>
/// <remarks>
/// Lazy on purpose. BulkAll pulls from this stream as it fills each request, so descriptions are
/// embedded a batch ahead of being sent rather than all five thousand up front -- the run holds
/// one batch of vectors at a time instead of the whole set, and the first documents reach the
/// cluster while the rest are still being encoded.
/// </remarks>
internal sealed class ListingVectorizer(IListingEmbedder embedder, int batchSize)
{
    private readonly Stopwatch _elapsed = new();

    /// <summary>Time spent inside the model, as opposed to parsing or indexing.</summary>
    public TimeSpan Elapsed => _elapsed.Elapsed;

    public int Embedded { get; private set; }

    public IEnumerable<Listing> Apply(IEnumerable<Listing> listings)
    {
        // Split from the iterator below so a null argument throws at the call rather than at the
        // first MoveNext, which is somewhere else entirely by then.
        ArgumentNullException.ThrowIfNull(listings);

        return Iterate(listings);
    }

    private IEnumerable<Listing> Iterate(IEnumerable<Listing> listings)
    {
        foreach (var batch in listings.Chunk(batchSize))
        {
            _elapsed.Start();

            var vectors = embedder.Embed([.. batch.Select(listing => listing.Description)]);

            _elapsed.Stop();

            Embedded += batch.Length;

            for (var index = 0; index < batch.Length; index++)
            {
                yield return batch[index] with { DescriptionVector = vectors[index] };
            }
        }
    }
}
