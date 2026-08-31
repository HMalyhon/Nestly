namespace Nestly.Domain;

/// <summary>Which retrieval leg surfaced a hit. Both legs matching is the strongest signal.</summary>
[Flags]
public enum MatchSource
{
    None = 0,
    Lexical = 1,
    Vector = 2,
    Both = Lexical | Vector,
}
