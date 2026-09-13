using Nestly.Seeder.Cleaning;

namespace Nestly.UnitTests.Cleaning;

public sealed class AmenityCatalogTests
{
    [Theory]
    [InlineData("Fast wifi -- 109 Mbps", "Wifi")]
    [InlineData("52 inch HDTV with Fire TV", "TV")]
    [InlineData("Central air conditioning", "Air conditioning")]
    [InlineData("Full kitchen", "Kitchen")]
    public void Normalize_LongTailValue_FoldsItOntoItsCanonicalTerm(string raw, string expected)
    {
        // Arrange
        var json = $"[\"{raw}\"]";

        // Act
        var normalized = AmenityCatalog.Normalize(json);

        // Assert
        Assert.Equal([expected], normalized);
    }

    [Theory]

    // Every one of these contains a shorter amenity's keyword. Without the exclusions a
    // dishwasher becomes a washer, a hair dryer becomes a dryer, and a whirlpool becomes a pool.
    [InlineData("Dishwasher", "Dishwasher", "Washer")]
    [InlineData("Hair dryer", null, "Dryer")]
    [InlineData("Whirlpool refrigerator", null, "Pool")]
    [InlineData("Pool table", null, "Pool")]
    public void Normalize_ValueContainingAShorterKeyword_DoesNotMatchTheShorterAmenity(string raw, string? expected, string mustNotMatch)
    {
        // Arrange
        var json = $"[\"{raw}\"]";

        // Act
        var normalized = AmenityCatalog.Normalize(json);

        // Assert
        Assert.DoesNotContain(mustNotMatch, normalized);

        if (expected is not null)
        {
            Assert.Contains(expected, normalized);
        }
    }

    [Fact]
    public void Normalize_GenuineWasherAndDryer_KeepsBoth()
    {
        // Arrange
        const string Json = "[\"Washer\",\"Dryer\"]";

        // Act
        var normalized = AmenityCatalog.Normalize(Json);

        // Assert
        Assert.Contains("Washer", normalized);
        Assert.Contains("Dryer", normalized);
    }

    [Fact]
    public void Normalize_UnmappableValue_DropsIt()
    {
        // Arrange -- an amenity no facet offers is dead weight in every document that carries it.
        // "Summit stainless steel oven" is the shape of the tail: specific enough that nobody
        // would filter by it, and naming no canonical term.
        const string Json = "[\"Ceiling fan\",\"Board games\",\"Summit stainless steel oven\"]";

        // Act
        var normalized = AmenityCatalog.Normalize(Json);

        // Assert
        Assert.Empty(normalized);
    }

    [Fact]
    public void Normalize_SeveralValuesFoldingTogether_ReturnsOne()
    {
        // Arrange
        const string Json = "[\"Wifi\",\"Fast wifi -- 109 Mbps\",\"Pocket wifi\"]";

        // Act
        var normalized = AmenityCatalog.Normalize(Json);

        // Assert
        Assert.Equal(["Wifi"], normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{\"not\":\"an array\"}")]
    public void Normalize_MalformedJson_ReturnsEmptyRatherThanFailingTheRow(string? raw)
    {
        // Act
        var normalized = AmenityCatalog.Normalize(raw);

        // Assert -- an unparseable amenity list is not a reason to drop a real apartment.
        Assert.Empty(normalized);
    }

    [Fact]
    public void Normalize_NullInsideTheArray_SkipsItRatherThanThrowing()
    {
        // A JSON null deserialises to a null element, which used to reach string.Replace.

        // Act
        var amenities = AmenityCatalog.Normalize("[\"Wifi\", null, \"Free parking on premises\"]");

        // Assert
        Assert.Contains("Wifi", amenities);
        Assert.Equal(2, amenities.Count);
    }
}
