using Microsoft.Extensions.DependencyInjection;
using Nestly.Domain;
using Nestly.Search.Searching;

namespace Nestly.IntegrationTests;

[Collection(SharedElasticsearch.Name)]
public sealed class HybridSearchTests(ElasticsearchFixture fixture)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private IListingSearchService Search => fixture.Services.GetRequiredService<IListingSearchService>();

    [Fact]
    public async Task Search_SemanticQuery_SurfacesListingsTheLexicalLegNeverReturned()
    {
        // The money test. MatchedBy is set from which leg returned the document, so a hit marked
        // Vector is one the lexical query did not find at any rank within its retrieval depth --
        // which is the whole claim of the hybrid layer, stated as an assertion.
        RequiresVectors();

        // Arrange
        var request = Request("quiet place near the park where a dog would be welcome");

        // Act
        var response = await Search.SearchAsync(request, Token);

        // Assert
        var vectorOnly = response.Hits.Where(hit => hit.MatchedBy == MatchSource.Vector).ToArray();

        Assert.True(
            vectorOnly.Length > 0,
            $"no hit was found by the vector leg alone; matchedBy was {Summarize(response)}");
    }

    [Fact]
    public async Task Search_SemanticQuery_RanksADocumentBothLegsFoundFirst()
    {
        // Arrange
        RequiresVectors();
        var request = Request("sunny apartment close to the subway");

        // Act
        var response = await Search.SearchAsync(request, Token);

        // Assert -- fusion is what puts agreement on top, and it is the first hit or nothing.
        Assert.NotEmpty(response.Hits);
        Assert.Equal(MatchSource.Both, response.Hits[0].MatchedBy);
    }

    [Fact]
    public async Task Search_SemanticQuery_ScoresInDescendingFusedOrder()
    {
        // Arrange
        RequiresVectors();
        var request = Request("bright loft with exposed brick");

        // Act
        var response = await Search.SearchAsync(request, Token);

        // Assert
        var scores = response.Hits.Select(hit => hit.Score).ToArray();

        Assert.Equal(scores.OrderByDescending(score => score), scores);
    }

    [Fact]
    public async Task Search_SemanticQueryWithAFilter_AppliesItToTheVectorLegToo()
    {
        // Without this a vector hit can be a listing in the wrong borough entirely: semantically
        // close, and exactly what the user excluded.
        RequiresVectors();

        // Arrange
        var borough = fixture.Seeded.GroupBy(listing => listing.Borough, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count()).First().Key;
        var request = Request("quiet apartment with a garden") with
        {
            Filters = new ListingFilters { Boroughs = [borough] },
            PageSize = 100,
        };

        // Act
        var response = await Search.SearchAsync(request, Token);

        // Assert -- including the hits only the vector leg found, which is where a missing
        // filter clause would show up.
        Assert.NotEmpty(response.Hits);
        Assert.All(response.Hits, hit => Assert.Equal(borough, hit.Listing.Borough));
    }

    [Fact]
    public async Task Search_SemanticQueryWithARentCeiling_KeepsTheVectorLegUnderIt()
    {
        // Arrange
        RequiresVectors();
        var ceiling = fixture.Seeded.OrderBy(listing => listing.MonthlyRent)
            .ElementAt(fixture.Seeded.Count / 4).MonthlyRent;
        var request = Request("spacious place for a family") with
        {
            Filters = new ListingFilters { MaxRent = ceiling },
            PageSize = 100,
        };

        // Act
        var response = await Search.SearchAsync(request, Token);

        // Assert
        Assert.NotEmpty(response.Hits);
        Assert.All(response.Hits, hit => Assert.True(hit.Listing.MonthlyRent <= ceiling));
    }

    [Fact]
    public async Task Search_EmptyQuery_SkipsTheVectorLegEntirely()
    {
        // Browsing has nothing to be semantically close to, so embedding the empty string and
        // running a kNN over it would be cost with no meaning attached.

        // Arrange
        var request = new ListingSearchRequest { Page = 1, PageSize = 20 };

        // Act
        var response = await Search.SearchAsync(request, Token);

        // Assert
        Assert.NotEmpty(response.Hits);
        Assert.All(response.Hits, hit => Assert.Equal(MatchSource.Lexical, hit.MatchedBy));
    }

    [Fact]
    public async Task Search_LexicalQuery_HighlightsTheWordsThatMatched()
    {
        // Arrange -- a word certain to be in the fixture, taken from it rather than guessed.
        var word = fixture.Seeded
            .SelectMany(listing => listing.Description.Split(' '))
            .Where(token => token.Length > 6 && token.All(char.IsLetter))
            .GroupBy(token => token, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .First().Key;

        // Act
        var response = await Search.SearchAsync(Request(word), Token);

        // Assert
        Assert.NotEmpty(response.Hits);
        Assert.Contains(response.Hits, hit => hit.Highlights.Count > 0);
        Assert.All(
            response.Hits.SelectMany(hit => hit.Highlights),
            fragment => Assert.Contains("<em>", fragment, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Search_QueryMatchingNothing_ReturnsNothingRatherThanEverything()
    {
        // Arrange
        var request = Request("zzzzqqqxx nonexistent gibberish token");

        // Act
        var response = await Search.SearchAsync(request, Token);

        // Assert -- with vectors on, kNN always has neighbours, so the guard is that the lexical
        // leg contributes none of them.
        Assert.DoesNotContain(response.Hits, hit => hit.MatchedBy.HasFlag(MatchSource.Lexical));
    }

    private static ListingSearchRequest Request(string query) =>
        new() { Query = query, Page = 1, PageSize = 20 };

    private static string Summarize(ListingSearchResponse response) =>
        string.Join(", ", response.Hits.GroupBy(hit => hit.MatchedBy)
            .Select(group => $"{group.Key}={group.Count()}"));

    /// <summary>Skips when the 90 MB model is not on disk, rather than failing the whole suite.</summary>
    private void RequiresVectors()
    {
        Assert.SkipUnless(fixture.HasVectors, "the ONNX model is not present; run tools/Nestly.ModelFetcher");
    }
}
