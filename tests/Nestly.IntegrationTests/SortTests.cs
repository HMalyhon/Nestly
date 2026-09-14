using Microsoft.Extensions.DependencyInjection;
using Nestly.Domain;
using Nestly.Search.Searching;

namespace Nestly.IntegrationTests;

[Collection(SharedElasticsearch.Name)]
public sealed class SortTests(ElasticsearchFixture fixture)
{
    private const int PageSize = 10;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private IListingSearchService Search => fixture.Services.GetRequiredService<IListingSearchService>();

    [Theory]
    [InlineData(ListingSort.PriceAsc)]
    [InlineData(ListingSort.PriceDesc)]
    public async Task Search_TextQuerySortedByPrice_OrdersTheResults(ListingSort sort)
    {
        // Elasticsearch sorts each retrieval leg on its own and cannot order their union, so the
        // hybrid path ignored Sort outright: asking for price ascending returned RRF order, and
        // ascending and descending came back identical.

        // Arrange
        var request = Request(1) with { Query = "loft", Sort = sort };

        // Act
        var response = await Search.SearchAsync(request, Token);

        // Assert
        var rents = response.Hits.Select(hit => hit.Listing.MonthlyRent).ToArray();
        var expected = sort == ListingSort.PriceAsc
            ? rents.OrderBy(rent => rent)
            : rents.OrderByDescending(rent => rent);

        Assert.NotEmpty(rents);
        Assert.Equal(expected, rents);
    }

    [Fact]
    public async Task Search_TextQuerySortedByPrice_OrdersAcrossPagesAndNotJustWithinThem()
    {
        // A sort applied to one page at a time looks right on page one and repeats listings on
        // page two, so the ordering has to be applied to the fused set before it is sliced.

        // Arrange
        var request = Request(1) with { Query = "loft", Sort = ListingSort.PriceAsc };

        // Act
        var first = await Search.SearchAsync(request, Token);
        var second = await Search.SearchAsync(request with { Page = 2 }, Token);

        // Assert
        var hits = first.Hits.Concat(second.Hits).ToArray();
        var rents = hits.Select(hit => hit.Listing.MonthlyRent).ToArray();
        var ids = hits.Select(hit => hit.Listing.Id).ToArray();

        Assert.Equal(rents.OrderBy(rent => rent), rents);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Search_SortedByPrice_StillCarriesHitsOnlyTheVectorLegFound()
    {
        // The reason the fused set is ordered rather than the sort being pushed down to one leg:
        // sorting by price must not quietly drop the semantic half of the search.
        RequiresVectors();

        // Arrange
        var request = Request(1) with
        {
            Query = "quiet place near the park where a dog would be welcome",
            Sort = ListingSort.PriceAsc,
        };

        // Act
        var response = await Search.SearchAsync(request, Token);

        // Assert
        Assert.Contains(response.Hits, hit => hit.MatchedBy == MatchSource.Vector);
    }

    [Fact]
    public async Task Search_SortedByRelevance_IsLeftInFusedOrder()
    {
        // Arrange
        var request = Request(1) with { Query = "loft", Sort = ListingSort.Relevance };

        // Act
        var response = await Search.SearchAsync(request, Token);

        // Assert -- RRF scores descend by construction, so any reordering shows up here.
        var scores = response.Hits.Select(hit => hit.Score).ToArray();

        Assert.NotEmpty(scores);
        Assert.Equal(scores.OrderByDescending(score => score), scores);
    }

    private static ListingSearchRequest Request(int page) => new() { Page = page, PageSize = PageSize };

    private void RequiresVectors()
    {
        Assert.SkipUnless(fixture.HasVectors, "the ONNX model is not present; run tools/Nestly.ModelFetcher");
    }
}
