using Microsoft.Extensions.DependencyInjection;
using Nestly.Domain;
using Nestly.Search.Searching;

namespace Nestly.IntegrationTests;

[Collection(SharedElasticsearch.Name)]
public sealed class GeoTests(ElasticsearchFixture fixture)
{
    /// <summary>Lower Manhattan, near enough the middle of the fixture's spread.</summary>
    private static readonly GeoPoint Centre = new(40.7280, -73.9950);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private IListingSearchService Search => fixture.Services.GetRequiredService<IListingSearchService>();

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task Search_WithinARadius_ReturnsOnlyListingsInsideIt(double radiusKm)
    {
        // Arrange
        var filters = new ListingFilters { Near = Centre, RadiusKm = radiusKm };
        var request = new ListingSearchRequest { Page = 1, PageSize = 100, Filters = filters };

        // Act
        var response = await Search.SearchAsync(request, Token);

        // Assert -- measured independently of Elasticsearch, with a little slack for the
        // difference between its spheroid and the haversine on a sphere.
        Assert.All(response.Hits, hit =>
            Assert.True(
                GeoDistance.Kilometers(Centre, hit.Listing.Location) <= radiusKm + 0.05,
                $"{hit.Listing.Id} is {GeoDistance.Kilometers(Centre, hit.Listing.Location):F2} km out"));
    }

    [Fact]
    public async Task Search_WithinARadius_ReturnsEveryListingInsideIt()
    {
        // The other direction: excluding too much is as wrong as excluding too little, and only
        // one of the two shows up in a spot check of the results.

        // Arrange
        const double RadiusKm = 5;
        var filters = new ListingFilters { Near = Centre, RadiusKm = RadiusKm };
        var request = new ListingSearchRequest { Page = 1, PageSize = 100, Filters = filters };

        // Act
        var response = await Search.SearchAsync(request, Token);

        // Assert
        var expected = fixture.Seeded
            .Count(listing => GeoDistance.Kilometers(Centre, listing.Location) <= RadiusKm - 0.05);

        Assert.True(response.Total >= expected, $"expected at least {expected} inside the radius, got {response.Total}");
    }

    [Fact]
    public async Task Search_AWiderRadius_NeverReturnsFewerListings()
    {
        // Arrange
        var near = new ListingFilters { Near = Centre, RadiusKm = 2 };
        var wide = new ListingFilters { Near = Centre, RadiusKm = 20 };

        // Act
        var narrow = await Search.SearchAsync(new ListingSearchRequest { Page = 1, PageSize = 1, Filters = near }, Token);
        var broad = await Search.SearchAsync(new ListingSearchRequest { Page = 1, PageSize = 1, Filters = wide }, Token);

        // Assert
        Assert.True(broad.Total >= narrow.Total);
    }

    [Fact]
    public async Task Search_WithinAViewport_ReturnsOnlyListingsInsideTheBox()
    {
        // Arrange -- roughly Brooklyn, which the fixture straddles.
        var bounds = new GeoBounds { TopLat = 40.72, BottomLat = 40.60, LeftLon = -74.05, RightLon = -73.90 };
        var request = new ListingSearchRequest
        {
            Page = 1,
            PageSize = 100,
            Filters = new ListingFilters { Within = bounds },
        };

        // Act
        var response = await Search.SearchAsync(request, Token);

        // Assert
        Assert.All(response.Hits, hit =>
        {
            var point = hit.Listing.Location;
            Assert.InRange(point.Lat, bounds.BottomLat, bounds.TopLat);
            Assert.InRange(point.Lon, bounds.LeftLon, bounds.RightLon);
        });
    }

    [Fact]
    public async Task Search_SortedByDistance_OrdersFromTheCentreOutwards()
    {
        // Arrange
        var request = new ListingSearchRequest
        {
            Page = 1,
            PageSize = 25,
            Sort = ListingSort.DistanceAsc,
            Filters = new ListingFilters { Near = Centre },
        };

        // Act
        var response = await Search.SearchAsync(request, Token);

        // Assert
        var distances = response.Hits.Select(hit => GeoDistance.Kilometers(Centre, hit.Listing.Location)).ToArray();

        Assert.Equal(distances.Order(), distances);
    }

    [Fact]
    public async Task Search_SortedByDistance_ReportsHowFarEachListingIs()
    {
        // Arrange
        var request = new ListingSearchRequest
        {
            Page = 1,
            PageSize = 5,
            Sort = ListingSort.DistanceAsc,
            Filters = new ListingFilters { Near = Centre },
        };

        // Act
        var response = await Search.SearchAsync(request, Token);

        // Assert
        Assert.All(response.Hits, hit =>
        {
            Assert.NotNull(hit.DistanceKm);
            Assert.Equal(GeoDistance.Kilometers(Centre, hit.Listing.Location), hit.DistanceKm.Value, 1);
        });
    }
}
