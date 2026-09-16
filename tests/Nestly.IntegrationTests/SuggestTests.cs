using Microsoft.Extensions.DependencyInjection;
using Nestly.Domain;
using Nestly.Search.Searching;

namespace Nestly.IntegrationTests;

[Collection(SharedElasticsearch.Name)]
public sealed class SuggestTests(ElasticsearchFixture fixture)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private IListingSuggestService Suggest => fixture.Services.GetRequiredService<IListingSuggestService>();

    private IListingSearchService Search => fixture.Services.GetRequiredService<IListingSearchService>();

    [Theory]
    [InlineData("loft")]
    [InlineData("room")]
    [InlineData("private")]
    [InlineData("cozy")]
    [InlineData("sunny")]
    public async Task Suggest_TitlePrefix_ListsEachTitleOnce(string prefix)
    {
        // Act
        var suggestions = await Suggest.SuggestAsync(prefix, Token);

        // Assert
        var titles = suggestions.Where(s => s.Kind == SuggestionKind.Listing).Select(s => s.Text).ToArray();

        Assert.NotEmpty(titles);
        Assert.Equal(titles.Length, titles.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Suggest_NeighborhoodPrefix_ListsPlacesBeforeListings()
    {
        // Arrange -- the typeahead groups by kind, which needs each kind in one unbroken run.
        const string Prefix = "bed";

        // Act
        var suggestions = await Suggest.SuggestAsync(Prefix, Token);

        // Assert
        var kinds = suggestions.Select(s => s.Kind).ToArray();

        Assert.Contains(SuggestionKind.Neighborhood, kinds);
        Assert.Equal(kinds.OrderBy(kind => kind == SuggestionKind.Listing), kinds);
    }

    [Fact]
    public async Task Suggest_Neighborhood_IsTheExactValueTheFilterMatches()
    {
        // The front end applies a picked neighbourhood as a filter on its text, and that filter is a
        // case-sensitive keyword match: a suggestion that differed by a letter would find nothing.

        // Arrange
        var suggestions = await Suggest.SuggestAsync("bed", Token);
        var neighborhood = suggestions.First(s => s.Kind == SuggestionKind.Neighborhood).Text;

        // Act
        var response = await Search.SearchAsync(
            new ListingSearchRequest { Filters = new ListingFilters { Neighborhoods = [neighborhood] } },
            Token);

        // Assert
        Assert.True(response.Total > 0, $"Filtering on the suggested \"{neighborhood}\" matched nothing.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Suggest_NothingTyped_ReturnsNothing(string query)
    {
        // Act
        var suggestions = await Suggest.SuggestAsync(query, Token);

        // Assert
        Assert.Empty(suggestions);
    }
}
