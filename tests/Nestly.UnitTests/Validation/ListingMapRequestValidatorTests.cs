using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Api.Validation;
using Nestly.Domain;

namespace Nestly.UnitTests.Validation;

public sealed class ListingMapRequestValidatorTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(12, true)]
    [InlineData(20, true)]
    [InlineData(0, false)]
    [InlineData(21, false)]
    public void Validate_Zoom_AcceptsOnlyLeafletsRasterRange(int zoom, bool valid)
    {
        // Zoom sizes the grid cells when results cluster, so a value outside the range is a
        // geotile precision Elasticsearch will reject.

        // Arrange
        var request = new ListingMapRequest { Zoom = zoom };
        var modelState = new ModelStateDictionary();

        // Act
        var result = ListingMapRequestValidator.TryValidate(request, modelState);

        // Assert
        Assert.Equal(valid, result);

        if (!valid)
        {
            Assert.Contains("Zoom", modelState.Keys);
        }
    }

    [Fact]
    public void Validate_InvertedRentRange_RejectsAsTheSearchDoes()
    {
        // The map and the list show the same result set, so a filter one accepts and the other
        // rejects would put them out of step.

        // Arrange
        var request = new ListingMapRequest
        {
            Zoom = 12,
            Filters = new ListingFilters { MinRent = 5000, MaxRent = 1000 },
        };
        var modelState = new ModelStateDictionary();

        // Act
        var result = ListingMapRequestValidator.TryValidate(request, modelState);

        // Assert
        Assert.False(result);
        Assert.Contains("MaxRent", modelState.Keys);
    }

    [Fact]
    public void Validate_ViewportOutsideRealCoordinates_Rejects()
    {
        // Arrange
        var bounds = new GeoBounds { TopLat = 41, BottomLat = 40, LeftLon = -190, RightLon = -73 };
        var request = new ListingMapRequest { Zoom = 12, Filters = new ListingFilters { Within = bounds } };
        var modelState = new ModelStateDictionary();

        // Act
        var result = ListingMapRequestValidator.TryValidate(request, modelState);

        // Assert
        Assert.False(result);
        Assert.Contains("Within", modelState.Keys);
    }

    [Fact]
    public void Validate_QueryOverTheLengthCap_RejectsAsTheSearchDoes()
    {
        // Arrange
        var request = new ListingMapRequest { Zoom = 12, Query = new string('a', 201) };
        var modelState = new ModelStateDictionary();

        // Act
        var result = ListingMapRequestValidator.TryValidate(request, modelState);

        // Assert
        Assert.False(result);
        Assert.Contains("Query", modelState.Keys);
    }

    [Fact]
    public void Validate_PlainViewportRequest_Accepts()
    {
        // Arrange
        var bounds = new GeoBounds { TopLat = 40.9, BottomLat = 40.5, LeftLon = -74.1, RightLon = -73.7 };
        var request = new ListingMapRequest { Zoom = 12, Filters = new ListingFilters { Within = bounds } };
        var modelState = new ModelStateDictionary();

        // Act
        var result = ListingMapRequestValidator.TryValidate(request, modelState);

        // Assert
        Assert.True(result);
    }
}
