using Microsoft.Extensions.DependencyInjection;
using Nestly.Domain;
using Nestly.Search.Searching;

namespace Nestly.IntegrationTests;

[Collection(SharedElasticsearch.Name)]
public sealed class PagingTests(ElasticsearchFixture fixture)
{
    private const int PageSize = 20;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private IListingSearchService Search => fixture.Services.GetRequiredService<IListingSearchService>();

    [Fact]
    public async Task Search_EveryPage_ReturnsEachListingExactlyOnce()
    {
        // A tie in the sort key that Elasticsearch breaks differently per request shows up here
        // as a listing on two pages and another on none.

        // Arrange -- the first ten pages rather than the whole corpus: a tie that reorders shows
        // up in the first few pages or not at all, and fifty searches would only be slower.
        const int Pages = 10;
        var seen = new List<string>();

        // Act
        for (var page = 1; page <= Pages; page++)
        {
            var response = await Search.SearchAsync(Request(page), Token);
            seen.AddRange(response.Hits.Select(hit => hit.Listing.Id));
        }

        // Assert
        Assert.Equal(Pages * PageSize, seen.Count);
        Assert.Equal(seen.Count, seen.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Search_SameRequestTwice_ReturnsTheSamePage()
    {
        // Arrange
        var request = Request(page: 3);

        // Act
        var first = await Search.SearchAsync(request, Token);
        var second = await Search.SearchAsync(request, Token);

        // Assert
        Assert.Equal(
            first.Hits.Select(hit => hit.Listing.Id),
            second.Hits.Select(hit => hit.Listing.Id));
    }

    [Fact]
    public async Task Search_SortedByPrice_OrdersAcrossPagesAndNotJustWithinThem()
    {
        // Arrange
        var request = Request(page: 1) with { Sort = ListingSort.PriceAsc };

        // Act
        var first = await Search.SearchAsync(request, Token);
        var second = await Search.SearchAsync(request with { Page = 2 }, Token);

        // Assert
        var prices = first.Hits.Concat(second.Hits).Select(hit => hit.Listing.MonthlyRent).ToArray();

        Assert.Equal(prices.OrderBy(price => price), prices);
    }

    [Fact]
    public async Task Search_PastTheLastPage_ReportsTheTotalWithNoHits()
    {
        // The UI tells "nothing matched" apart from "you paged past the end" by exactly this.

        // Arrange -- 1,000 listings at twenty a page is exactly fifty pages, so page fifty is the
        // last one rather than past the end.
        var request = Request(page: 60);

        // Act
        var response = await Search.SearchAsync(request, Token);

        // Assert
        Assert.Equal(ElasticsearchFixture.ListingCount, response.Total);
        Assert.Empty(response.Hits);
    }

    [Fact]
    public async Task Search_LastPartialPage_ReturnsOnlyTheRemainder()
    {
        // Arrange -- 1,000 listings, 30 to a page, so the thirty-fourth page holds 10.
        var request = new ListingSearchRequest { Page = 34, PageSize = 30 };

        // Act
        var response = await Search.SearchAsync(request, Token);

        // Assert
        Assert.Equal(10, response.Hits.Count);
    }

    private static ListingSearchRequest Request(int page) => new() { Page = page, PageSize = PageSize };
}
