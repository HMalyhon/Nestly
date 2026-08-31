namespace Nestly.Domain;

/// <summary>One facet value and how many listings carry it.</summary>
public readonly record struct FacetBucket(string Key, long Count);
