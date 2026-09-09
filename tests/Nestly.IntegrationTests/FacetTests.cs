using Microsoft.Extensions.DependencyInjection;
using Nestly.Domain;
using Nestly.Search.Searching;

namespace Nestly.IntegrationTests;

[Collection(SharedElasticsearch.Name)]
public sealed class FacetTests(ElasticsearchFixture fixture)
{
    /// <summary>The runner's own token, so an interrupted run stops mid-request rather than at the end.</summary>
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private IListingSearchService Search => fixture.Services.GetRequiredService<IListingSearchService>();

    [Fact]
    public async Task Search_NoFilters_CountsEveryBoroughInTheFixture()
    {
        // Arrange
        var request = Request();

        // Act
        var response = await Search.SearchAsync(request, Token);

        // Assert -- the facet counts have to add up to the corpus, or they are not counts.
        var expected = fixture.Seeded.GroupBy(listing => listing.Borough, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        Assert.Equal(ElasticsearchFixture.ListingCount, response.Total);

        foreach (var bucket in response.Facets.Boroughs)
        {
            Assert.Equal(expected[bucket.Key], bucket.Count);
        }
    }

    [Fact]
    public async Task Search_BoroughSelected_StillCountsTheOtherBoroughs()
    {
        // The defect this guards: a borough facet computed with the borough filter applied
        // reports the selected borough and zero for everything else, so nobody can widen a
        // search without clearing it first.

        // Arrange
        var borough = fixture.Seeded.GroupBy(listing => listing.Borough, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count()).First().Key;
        var request = Request(new ListingFilters { Boroughs = [borough] });

        // Act
        var response = await Search.SearchAsync(request, Token);

        // Assert
        Assert.True(response.Total > 0);
        Assert.True(response.Facets.Boroughs.Count > 1, "the unselected boroughs should still be offered");
        Assert.Contains(response.Facets.Boroughs, bucket => !string.Equals(bucket.Key, borough, StringComparison.Ordinal) && bucket.Count > 0);
    }

    [Fact]
    public async Task Search_BoroughSelected_CountsTheOtherBoroughsAsIfItWereNotSelected()
    {
        // Arrange
        var borough = fixture.Seeded.GroupBy(listing => listing.Borough, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count()).First().Key;

        // Act
        var unfiltered = await Search.SearchAsync(Request(), Token);
        var filtered = await Search.SearchAsync(Request(new ListingFilters { Boroughs = [borough] }), Token);

        // Assert -- excluding its own dimension means the borough counts are unchanged by it.
        var before = unfiltered.Facets.Boroughs.ToDictionary(b => b.Key, b => b.Count, StringComparer.Ordinal);
        var after = filtered.Facets.Boroughs.ToDictionary(b => b.Key, b => b.Count, StringComparer.Ordinal);

        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Search_BoroughSelected_NarrowsTheNeighborhoodCounts()
    {
        // The other half of the same rule: a facet that is not the selected dimension must be
        // counted with that filter applied, or it promises results the search cannot deliver.

        // Arrange
        var borough = fixture.Seeded.GroupBy(listing => listing.Borough, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count()).First().Key;

        // Act
        var response = await Search.SearchAsync(Request(new ListingFilters { Boroughs = [borough] }), Token);

        // Assert
        var expected = fixture.Seeded
            .Where(listing => string.Equals(listing.Borough, borough, StringComparison.Ordinal))
            .Select(listing => listing.Neighborhood)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(response.Facets.Neighborhoods);
        Assert.All(response.Facets.Neighborhoods, bucket => Assert.Contains(bucket.Key, expected));
    }

    [Fact]
    public async Task Search_AmenitySelected_CountsAmenitiesWithItStillApplied()
    {
        // Amenities are conjunctive, so their facet keeps its own filter: a count reads "how many
        // of my current results also have this", which is the number that answers whether
        // ticking it is worth it. Excluding it would show the whole corpus beside a filtered list.

        // Arrange
        var amenity = fixture.Seeded.SelectMany(listing => listing.Amenities)
            .GroupBy(name => name, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count()).First().Key;

        // Act
        var response = await Search.SearchAsync(Request(new ListingFilters { Amenities = [amenity] }), Token);

        // Assert
        var selected = Assert.Single(response.Facets.Amenities, bucket => string.Equals(bucket.Key, amenity, StringComparison.Ordinal));

        Assert.Equal(response.Total, selected.Count);
        Assert.All(response.Facets.Amenities, bucket => Assert.True(bucket.Count <= response.Total));
    }

    [Fact]
    public async Task Search_TwoAmenities_NarrowsRatherThanWidens()
    {
        // Arrange
        var amenities = fixture.Seeded.SelectMany(listing => listing.Amenities)
            .GroupBy(name => name, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count()).Take(2).Select(group => group.Key).ToArray();

        // Act
        var one = await Search.SearchAsync(Request(new ListingFilters { Amenities = [amenities[0]] }), Token);
        var both = await Search.SearchAsync(Request(new ListingFilters { Amenities = amenities }), Token);

        // Assert
        Assert.True(both.Total <= one.Total, "a second amenity must not widen the result set");
    }

    [Fact]
    public async Task Search_RentRange_ReportsBoundsThatIgnoreTheRentFilter()
    {
        // The slider's track comes from these, so narrowing the range must not shrink the track
        // under the handle that set it.

        // Act
        var unfiltered = await Search.SearchAsync(Request(), Token);
        var filtered = await Search.SearchAsync(Request(new ListingFilters { MinRent = 3000 }), Token);

        // Assert
        Assert.Equal(unfiltered.Facets.MinRent, filtered.Facets.MinRent);
        Assert.Equal(unfiltered.Facets.MaxRent, filtered.Facets.MaxRent);
    }

    private static ListingSearchRequest Request(ListingFilters? filters = null) =>
        new() { Page = 1, PageSize = 20, Filters = filters ?? new ListingFilters() };
}
