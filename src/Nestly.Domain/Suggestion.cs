namespace Nestly.Domain;

/// <summary>One autocomplete row.</summary>
public readonly record struct Suggestion(string Text, SuggestionKind Kind);
