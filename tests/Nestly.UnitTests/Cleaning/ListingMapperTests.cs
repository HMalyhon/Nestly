using Nestly.Seeder.Cleaning;
using Nestly.Seeder.Csv;

namespace Nestly.UnitTests.Cleaning;

public sealed class ListingMapperTests
{
    [Theory]
    [InlineData("$1,250.00", 1250)]
    [InlineData("$95", 95)]
    [InlineData("1250", 1250)]
    [InlineData("$1,250.50", 1251)]
    [InlineData("$0.49", 0)]
    public void TryParsePrice_PublishedLiteral_ReadsWholeDollars(string raw, int expected)
    {
        // Act
        var parsed = ListingMapper.TryParsePrice(raw, out var price);

        // Assert
        Assert.True(parsed);
        Assert.Equal(expected, price);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("free")]
    [InlineData("$")]
    public void TryParsePrice_NotAPrice_Rejects(string? raw)
    {
        // Act
        var parsed = ListingMapper.TryParsePrice(raw, out _);

        // Assert
        Assert.False(parsed);
    }

    [Theory]
    [InlineData("1 bath", 1)]
    [InlineData("2.5 baths", 2.5)]
    [InlineData("1 shared bath", 1)]
    [InlineData("1.5 private baths", 1.5)]
    public void TryParseBathrooms_SharedOrPrivateWording_TakesOnlyTheCount(string raw, decimal expected)
    {
        // Act
        var parsed = ListingMapper.TryParseBathrooms(raw, out var bathrooms);

        // Assert
        Assert.True(parsed);
        Assert.Equal(expected, bathrooms);
    }

    [Theory]
    [InlineData("Half-bath")]
    [InlineData("Private half-bath")]
    [InlineData("Shared half-bath")]
    public void TryParseBathrooms_HalfBathCarryingNoDigit_ReadsItAsAHalf(string raw)
    {
        // Act
        var parsed = ListingMapper.TryParseBathrooms(raw, out var bathrooms);

        // Assert -- rejecting these would drop a listing that does have a bathroom, just half of one.
        Assert.True(parsed);
        Assert.Equal(0.5m, bathrooms);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bath")]
    public void TryParseBathrooms_NoCountAtAll_Rejects(string? raw)
    {
        // Act
        var parsed = ListingMapper.TryParseBathrooms(raw, out _);

        // Assert
        Assert.False(parsed);
    }

    [Fact]
    public void TryMap_NightlyRate_DerivesTheMonthlyRent()
    {
        // Arrange
        var row = Row();

        // Act
        var mapped = ListingMapper.TryMap(row, out var listing, out _);

        // Assert -- the one invented value in the project, and the README says so. Thirty flat.
        Assert.True(mapped);
        Assert.Equal(100, listing.PricePerNight);
        Assert.Equal(3000, listing.MonthlyRent);
    }

    [Theory]

    // Elasticsearch's byte is signed, so 127 is the ceiling. A listing claiming 900 bedrooms is
    // bad data, and clamping it to something plausible is how bad data stops looking like it.
    [InlineData("127", true)]
    [InlineData("128", false)]
    [InlineData("900", false)]
    [InlineData("-1", false)]
    [InlineData("1.0", true)]
    public void TryMap_BedroomCount_AcceptsOnlyWhatTheIndexCanHold(string bedrooms, bool expected)
    {
        // Arrange
        var row = Row(bedrooms: bedrooms);

        // Act
        var mapped = ListingMapper.TryMap(row, out _, out var reason);

        // Assert
        Assert.Equal(expected, mapped);

        if (!expected)
        {
            Assert.Equal(ListingSkipReason.Bedrooms, reason);
        }
    }

    [Theory]

    // The reason is compared by name because the enum is internal to the seeder, and a public
    // test method cannot take it as a parameter.
    [InlineData("", null, null, "Identifier")]
    [InlineData("1", "not-a-number", null, "Coordinates")]
    [InlineData("1", null, "free", "Price")]
    public void TryMap_UnusableRow_ReportsWhyItWasDropped(string id, string? latitude, string? price, string expected)
    {
        // Arrange
        var row = Row() with { Id = id };
        row = latitude is null ? row : row with { Latitude = latitude };
        row = price is null ? row : row with { Price = price };

        // Act
        var mapped = ListingMapper.TryMap(row, out _, out var reason);

        // Assert
        Assert.False(mapped);
        Assert.Equal(expected, reason.ToString());
    }

    [Fact]
    public void TryMap_DescriptionOfNothingButMarkup_DropsTheRow()
    {
        // Arrange
        var row = Row() with { Description = "<br /><p></p>" };

        // Act
        var mapped = ListingMapper.TryMap(row, out _, out var reason);

        // Assert -- cleaned first, then checked: markup-only is as useless to search as empty,
        // and both legs would index the noise.
        Assert.False(mapped);
        Assert.Equal(ListingSkipReason.Description, reason);
    }

    [Fact]
    public void TryMap_AbsentReviewScore_MapsAsUnratedRatherThanMalformed()
    {
        // Arrange
        var row = Row() with { ReviewScore = string.Empty };

        // Act
        var mapped = ListingMapper.TryMap(row, out var listing, out _);

        // Assert -- a listing with no reviews yet is a real listing.
        Assert.True(mapped);
        Assert.Null(listing.ReviewScore);
    }

    [Fact]
    public void TryMap_UnreadableMinimumNights_FallsBackToOneNight()
    {
        // Arrange
        var row = Row() with { MinimumNights = "n/a" };

        // Act
        var mapped = ListingMapper.TryMap(row, out var listing, out _);

        // Assert
        Assert.True(mapped);
        Assert.Equal((short)1, listing.MinimumNights);
    }

    [Fact]
    public void TryMap_MarkupInTitleAndDescription_StoresThemCleaned()
    {
        // Arrange
        var row = Row() with { Name = "Loft &amp; garden", Description = "Bright<br />and airy" };

        // Act
        var mapped = ListingMapper.TryMap(row, out var listing, out _);

        // Assert
        Assert.True(mapped);
        Assert.Equal("Loft & garden", listing.Title);
        Assert.Equal("Bright and airy", listing.Description);
    }

    private static ListingCsvRow Row(string bedrooms = "1") => new()
    {
        Id = "12345",
        Name = "Sunny loft",
        Description = "A bright apartment near the park.",
        Neighborhood = "Bushwick",
        Borough = "Brooklyn",
        Latitude = "40.7",
        Longitude = "-73.9",
        PropertyType = "Entire rental unit",
        RoomType = "Entire home/apt",
        Accommodates = "2",
        BathroomsText = "1 bath",
        Bedrooms = bedrooms,
        Amenities = "[\"Wifi\"]",
        Price = "$100.00",
        MinimumNights = "30",
        ReviewScore = "4.8",
        LastReview = "2026-06-01",
    };
}
