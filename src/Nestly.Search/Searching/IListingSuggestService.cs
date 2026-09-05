using Nestly.Domain;

namespace Nestly.Search.Searching;

/// <summary>Autocomplete for the search box.</summary>
public interface IListingSuggestService
{
    Task<IReadOnlyList<Suggestion>> SuggestAsync(string query, CancellationToken cancellationToken = default);
}
