using Nestly.Domain;
using Nestly.Search.Searching;

namespace Nestly.UnitTests.Searching;

public sealed class GeoDistanceTests
{
    private static readonly GeoPoint TimesSquare = new(40.7580, -73.9855);
    private static readonly GeoPoint ProspectPark = new(40.6602, -73.9690);

    [Fact]
    public void Kilometers_TwoPointsAcrossTheCity_MatchesTheKnownDistance()
    {
        // Arrange -- Times Square to Prospect Park is a shade under 11 km as the crow flies:
        // 0.0978 degrees of latitude is 10.87 km, and 0.0165 of longitude at this latitude adds
        // 1.39 km. A spherical earth is metres out over that distance, which a "1.2 km away"
        // label will never notice.

        // Act
        var distance = GeoDistance.Kilometers(TimesSquare, ProspectPark);

        // Assert -- ranged rather than exact, so the test states the geography and not the
        // implementation's last decimal.
        Assert.InRange(distance, 10.9, 11.0);
    }

    [Fact]
    public void Kilometers_ThePointItself_IsZero()
    {
        // Act
        var distance = GeoDistance.Kilometers(TimesSquare, TimesSquare);

        // Assert
        Assert.Equal(0, distance, 9);
    }

    [Fact]
    public void Kilometers_ReversedArguments_ReturnsTheSameDistance()
    {
        // Act
        var there = GeoDistance.Kilometers(TimesSquare, ProspectPark);
        var back = GeoDistance.Kilometers(ProspectPark, TimesSquare);

        // Assert
        Assert.Equal(there, back, 9);
    }

    [Fact]
    public void Kilometers_Antipodes_StaysInsideTheDomainOfAsin()
    {
        // Arrange -- rounding can push the haversine term just past 1, and Asin of that is NaN.
        var origin = new GeoPoint(0, 0);
        var opposite = new GeoPoint(0, 180);

        // Act
        var distance = GeoDistance.Kilometers(origin, opposite);

        // Assert
        Assert.False(double.IsNaN(distance));
        Assert.Equal(20015, distance, 0);
    }
}
