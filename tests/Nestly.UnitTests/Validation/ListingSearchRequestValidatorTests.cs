using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Api.Validation;
using Nestly.Domain;

namespace Nestly.UnitTests.Validation;

public sealed class ListingSearchRequestValidatorTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(0, false)]
    [InlineData(101, false)]
    public void Validate_Page_AcceptsOnlyWhatIsWithinTheCap(int page, bool expected)
    {
        // Arrange
        var request = new ListingSearchRequest { Page = page, PageSize = 20 };

        // Act
        var valid = Validate(request, out _);

        // Assert
        Assert.Equal(expected, valid);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(0, false)]
    [InlineData(101, false)]
    public void Validate_PageSize_AcceptsOnlyWhatIsWithinTheCap(int pageSize, bool expected)
    {
        // Arrange
        var request = new ListingSearchRequest { Page = 1, PageSize = pageSize };

        // Act
        var valid = Validate(request, out _);

        // Assert
        Assert.Equal(expected, valid);
    }

    [Fact]
    public void Validate_QueryOverTheLengthCap_Rejects()
    {
        // Arrange -- every token becomes a fuzzy clause across three fields, so cost is linear
        // in length: 500 words takes 18 seconds and 1,000 times out. A search box, not an essay.
        var atTheCap = Request() with { Query = new string('a', 200) };
        var overIt = Request() with { Query = new string('a', 201) };

        // Act
        var capValid = Validate(atTheCap, out _);
        var overValid = Validate(overIt, out var errors);

        // Assert
        Assert.True(capValid);
        Assert.False(overValid);
        Assert.Contains("Query", errors);
    }

    [Fact]
    public void Validate_ViewportWhoseTopIsBelowItsBottom_Rejects()
    {
        // Arrange
        var inverted = new GeoBounds { TopLat = 40.5, BottomLat = 40.8, LeftLon = -74, RightLon = -73 };
        var request = Request(new ListingFilters { Within = inverted });

        // Act
        var valid = Validate(request, out var errors);

        // Assert
        Assert.False(valid);
        Assert.Contains("Within", errors);
    }

    [Theory]
    [InlineData(-190d, -73d)]
    [InlineData(-74d, 190d)]
    public void Validate_ViewportOutsideRealCoordinates_Rejects(double left, double right)
    {
        // Arrange -- unchecked, a viewport clamped past a pole or wrapped past the antimeridian
        // reaches Elasticsearch and fails there instead.
        var bounds = new GeoBounds { TopLat = 41, BottomLat = 40, LeftLon = left, RightLon = right };
        var request = Request(new ListingFilters { Within = bounds });

        // Act
        var valid = Validate(request, out var errors);

        // Assert
        Assert.False(valid);
        Assert.Contains("Within", errors);
    }

    [Fact]
    public void Validate_RealViewport_Accepts()
    {
        // Arrange
        var bounds = new GeoBounds { TopLat = 40.9, BottomLat = 40.5, LeftLon = -74.1, RightLon = -73.7 };
        var request = Request(new ListingFilters { Within = bounds });

        // Act
        var valid = Validate(request, out _);

        // Assert
        Assert.True(valid);
    }

    [Fact]
    public void Validate_MaximumRentBelowTheMinimum_Rejects()
    {
        // Arrange
        var request = Request(new ListingFilters { MinRent = 5000, MaxRent = 1000 });

        // Act
        var valid = Validate(request, out var errors);

        // Assert
        Assert.False(valid);
        Assert.Contains("MaxRent", errors);
    }

    [Fact]
    public void Validate_MaximumRentEqualToTheMinimum_Accepts()
    {
        // Arrange
        var request = Request(new ListingFilters { MinRent = 2000, MaxRent = 2000 });

        // Act
        var valid = Validate(request, out _);

        // Assert
        Assert.True(valid);
    }

    [Theory]
    [InlineData(-1)]
    public void Validate_NegativeRent_Rejects(int rent)
    {
        // Arrange
        var request = Request(new ListingFilters { MinRent = rent });

        // Act
        var valid = Validate(request, out var errors);

        // Assert
        Assert.False(valid);
        Assert.Contains("MinRent", errors);
    }

    [Fact]
    public void Validate_MoreAmenitiesThanTheVocabularyHolds_Rejects()
    {
        // Arrange
        var amenities = Enumerable.Range(0, 21).Select(index => $"amenity-{index}").ToArray();
        var request = Request(new ListingFilters { Amenities = amenities });

        // Act
        var valid = Validate(request, out var errors);

        // Assert
        Assert.False(valid);
        Assert.Contains("Amenities", errors);
    }

    [Fact]
    public void Validate_OverlongFilterList_Rejects()
    {
        // Arrange
        var boroughs = Enumerable.Range(0, 51).Select(index => $"borough-{index}").ToArray();
        var request = Request(new ListingFilters { Boroughs = boroughs });

        // Act
        var valid = Validate(request, out var errors);

        // Assert
        Assert.False(valid);
        Assert.Contains("Boroughs", errors);
    }

    [Fact]
    public void Validate_DistanceSortWithNoCentre_Rejects()
    {
        // Arrange
        var request = Request() with { Sort = ListingSort.DistanceAsc };

        // Act
        var valid = Validate(request, out var errors);

        // Assert
        Assert.False(valid);
        Assert.Contains("Sort", errors);
    }

    [Fact]
    public void Validate_DistanceSortWithACentre_Accepts()
    {
        // Arrange
        var filters = new ListingFilters { Near = new GeoPoint(40.7, -73.9) };
        var request = Request(filters) with { Sort = ListingSort.DistanceAsc };

        // Act
        var valid = Validate(request, out _);

        // Assert
        Assert.True(valid);
    }

    [Fact]
    public void Validate_RadiusWithNoCentre_Rejects()
    {
        // Arrange -- a radius without a centre is a caller bug, not a default to guess at.
        var request = Request(new ListingFilters { RadiusKm = 5 });

        // Act
        var valid = Validate(request, out var errors);

        // Assert
        Assert.False(valid);
        Assert.Contains("Near", errors);
    }

    [Fact]
    public void Validate_SeveralBrokenRules_ReportsThemAllAtOnce()
    {
        // Arrange -- one round trip should tell the caller everything wrong with the request.
        var request = new ListingSearchRequest
        {
            Page = 0,
            PageSize = 0,
            Query = new string('a', 201),
        };

        // Act
        var valid = Validate(request, out var errors);

        // Assert
        Assert.False(valid);
        Assert.Equal(3, errors.Count);
    }

    private static ListingSearchRequest Request(ListingFilters? filters = null) =>
        new() { Page = 1, PageSize = 20, Filters = filters ?? new ListingFilters() };

    private static bool Validate(ListingSearchRequest request, out IReadOnlyCollection<string> errors)
    {
        var modelState = new ModelStateDictionary();
        var valid = ListingSearchRequestValidator.TryValidate(request, modelState);

        errors = [.. modelState.Keys];
        return valid;
    }
}
