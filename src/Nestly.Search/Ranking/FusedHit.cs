using Nestly.Domain;

namespace Nestly.Search.Ranking;

/// <summary>One document's place in the fused ranking, and which retrieval leg put it there.</summary>
public readonly record struct FusedHit(string Id, double Score, MatchSource MatchedBy);
