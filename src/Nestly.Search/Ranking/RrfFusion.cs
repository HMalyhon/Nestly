using Nestly.Domain;

namespace Nestly.Search.Ranking;

/// <summary>
/// Reciprocal Rank Fusion: merges two ranked lists into one.
/// </summary>
/// <remarks>
/// <para>
/// The two legs cannot be added together. BM25 returns unbounded scores that move with the corpus
/// and the query, kNN returns a normalised cosine score in [0, 1], and no constant reconciles them.
/// RRF throws the scores away and keeps only the positions, which is the one thing the two lists
/// genuinely have in common.
/// </para>
/// <para>
/// Elasticsearch has an <c>rrf</c> retriever that does this, and it requires an Enterprise
/// licence. The fifty lines below keep the demo running forever on Basic.
/// </para>
/// </remarks>
public static class RrfFusion
{
    /// <summary>
    /// The rank-smoothing constant from Cormack et al. (2009), who found it insensitive enough
    /// that 60 has been the default everywhere since.
    /// </summary>
    // It flattens the top of each list: 1/61 against 1/62 is a small gap, so being first in one
    // leg does not outvote appearing in both, which is the signal worth ranking on.
    public const int DefaultK = 60;

    /// <summary>
    /// Fuses two ranked lists of document ids, best first.
    /// </summary>
    /// <param name="lexical">Ids from the BM25 leg, in rank order.</param>
    /// <param name="vector">Ids from the kNN leg, in rank order.</param>
    /// <param name="k">Rank-smoothing constant; see <see cref="DefaultK"/>.</param>
    public static IReadOnlyList<FusedHit> Fuse(
        IReadOnlyList<string> lexical,
        IReadOnlyList<string> vector,
        int k = DefaultK)
    {
        ArgumentNullException.ThrowIfNull(lexical);
        ArgumentNullException.ThrowIfNull(vector);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(k);

        var scores = new Dictionary<string, Entry>(lexical.Count + vector.Count, StringComparer.Ordinal);

        Accumulate(scores, lexical, MatchSource.Lexical, k);
        Accumulate(scores, vector, MatchSource.Vector, k);

        return
        [
            .. scores.Values
                .OrderByDescending(entry => entry.Score)

                // Ties break on the order the document was first seen, so the same two inputs
                // always produce the same page -- which is what makes deep paging stable.
                .ThenBy(entry => entry.FirstSeen)
                .Select(entry => new FusedHit(entry.Id, entry.Score, entry.MatchedBy)),
        ];
    }

    private static void Accumulate(
        Dictionary<string, Entry> scores,
        IReadOnlyList<string> ranking,
        MatchSource source,
        int k)
    {
        for (var index = 0; index < ranking.Count; index++)
        {
            var id = ranking[index];

            // Rank is one-based: the top document scores 1/(k+1), not 1/k.
            var contribution = 1d / (k + index + 1);

            if (scores.TryGetValue(id, out var existing))
            {
                scores[id] = existing with
                {
                    Score = existing.Score + contribution,
                    MatchedBy = existing.MatchedBy | source,
                };
            }
            else
            {
                scores[id] = new Entry(id, contribution, source, scores.Count);
            }
        }
    }

    private readonly record struct Entry(string Id, double Score, MatchSource MatchedBy, int FirstSeen);
}
