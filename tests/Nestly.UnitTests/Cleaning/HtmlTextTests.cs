using Nestly.Seeder.Cleaning;

namespace Nestly.UnitTests.Cleaning;

public sealed class HtmlTextTests
{
    [Fact]
    public void Clean_EncodedMarkup_DecodesBeforeStrippingSoNoTagSurvives()
    {
        // Arrange -- the ordering defect this pins down: strip-then-decode leaves the entities
        // untouched, because the tag pattern cannot see them, and the decode afterwards hands
        // back live markup.
        const string Raw = "&lt;img src=x onerror=alert(1)&gt;Cosy studio";

        // Act
        var cleaned = HtmlText.Clean(Raw);

        // Assert
        Assert.Equal("Cosy studio", cleaned);
        Assert.DoesNotContain("<", cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain("onerror", cleaned, StringComparison.Ordinal);
    }

    [Fact]
    public void Clean_ProseThatLooksLikeAnOpeningTag_KeepsTheSentence()
    {
        // Arrange -- against a loose <[^>]+> pattern the bracket in "<25 lbs" pairs with the next
        // real tag's closing bracket, and the sentence between them disappears.
        const string Raw = "Dogs <25 lbs welcome.<br />Quiet street.";

        // Act
        var cleaned = HtmlText.Clean(Raw);

        // Assert
        Assert.Equal("Dogs <25 lbs welcome. Quiet street.", cleaned);
    }

    [Fact]
    public void Clean_TagBetweenTwoWords_ReplacesItWithASpace()
    {
        // Arrange -- deleting the tag outright would index "kitchenBedroom" as one term, which
        // no query for either word would match.
        const string Raw = "kitchen<br />Bedroom";

        // Act
        var cleaned = HtmlText.Clean(Raw);

        // Assert
        Assert.Equal("kitchen Bedroom", cleaned);
    }

    [Fact]
    public void Clean_RunsOfWhitespace_CollapsesThemIncludingNonBreakingSpaces()
    {
        // Arrange
        const string Raw = "  Sunny\n\n   loft&nbsp;&nbsp; ";

        // Act
        var cleaned = HtmlText.Clean(Raw);

        // Assert
        Assert.Equal("Sunny loft", cleaned);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<br /><p></p>")]
    public void Clean_NothingWorthIndexing_ReturnsEmpty(string? raw)
    {
        // Act
        var cleaned = HtmlText.Clean(raw);

        // Assert
        Assert.Equal(string.Empty, cleaned);
    }

    [Fact]
    public void Clean_PlainText_LeavesItAlone()
    {
        // Arrange
        const string Raw = "Two bedrooms in Bushwick";

        // Act
        var cleaned = HtmlText.Clean(Raw);

        // Assert
        Assert.Equal(Raw, cleaned);
    }
}
