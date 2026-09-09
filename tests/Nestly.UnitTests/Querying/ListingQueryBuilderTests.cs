using System.Text.Json;
using Nestly.Domain;
using Nestly.Search.Querying;

namespace Nestly.UnitTests.Querying;

public sealed class ListingQueryBuilderTests
{
    [Fact]
    public void Build_NoQueryAndNoFilters_MatchesEverything()
    {
        // Arrange
        var filters = new ListingFilters();

        // Act
        var json = Dsl.Of(ListingQueryBuilder.Build(query: null, filters));

        // Assert
        Assert.True(json.TryGetProperty("match_all", out _));
    }

    [Fact]
    public void Build_QueryAndFilters_PutsTheFiltersWhereTheyDoNotScore()
    {
        // Arrange
        var filters = new ListingFilters { Boroughs = ["Brooklyn"] };

        // Act
        var json = Dsl.Of(ListingQueryBuilder.Build("loft", filters));

        // Assert
        var boolQuery = json.GetProperty("bool");
        Assert.True(Assert.Single(Dsl.Clauses(boolQuery, "filter")).TryGetProperty("terms", out _));
        Assert.True(Assert.Single(Dsl.Clauses(boolQuery, "must")).TryGetProperty("multi_match", out _));
    }

    [Fact]
    public void Build_FiltersAndNoText_ScoresNothing()
    {
        // Arrange
        var filters = new ListingFilters { Boroughs = ["Brooklyn"] };

        // Act
        var json = Dsl.Of(ListingQueryBuilder.Build(query: null, filters));

        // Assert
        Assert.True(json.GetProperty("bool").TryGetProperty("filter", out _));
        Assert.False(json.GetProperty("bool").TryGetProperty("must", out _));
    }

    [Fact]
    public void Text_AnyQuery_BoostsTheTitleOverTheDescription()
    {
        // A listing whose name says "sunny studio" is a better answer than one that scatters the
        // words across three fields.

        // Act
        var json = Dsl.Of(ListingQueryBuilder.Text("sunny studio"));

        // Assert
        var fields = json.GetProperty("multi_match").GetProperty("fields")
            .EnumerateArray().Select(field => field.GetString()).ToArray();

        Assert.Contains("title^3", fields);
        Assert.Contains("description", fields);
    }

    [Fact]
    public void Text_AnyQuery_ToleratesOneTypoButNotInTheOpeningLetters()
    {
        // Without a prefix length every term walks a wide slice of the term dictionary, which is
        // what made a long query take a second.

        // Act
        var json = Dsl.Of(ListingQueryBuilder.Text("brooklin"));

        // Assert
        var match = json.GetProperty("multi_match");
        Assert.Equal("AUTO", match.GetProperty("fuzziness").GetString());
        Assert.Equal(2, match.GetProperty("prefix_length").GetInt32());
    }

    [Fact]
    public void HighlightText_AnyQuery_DropsTheFuzzinessThatCostsTenTimesTheSearch()
    {
        // 149 ms against 14 ms over one page, and a fuzzy match is by definition not the word
        // that was typed.

        // Act
        var match = Dsl.Of(ListingQueryBuilder.HighlightText("brooklin")).GetProperty("multi_match");

        // Assert
        Assert.False(match.TryGetProperty("fuzziness", out _));
        Assert.False(match.TryGetProperty("prefix_length", out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Text_BlankQuery_ReturnsNull(string? query)
    {
        // Act
        var text = ListingQueryBuilder.Text(query);

        // Assert
        Assert.Null(text);
    }

    [Fact]
    public void Filters_SeveralAmenities_BuildsAConjunctionThatNarrowsResults()
    {
        // Every other keyword filter is disjunctive. Ticking "Elevator" and "Washer" asks for
        // both, which a terms query would not do.

        // Arrange
        var filters = new ListingFilters { Amenities = ["Elevator", "Washer"] };

        // Act
        var json = Dsl.Of(Assert.Single(ListingQueryBuilder.Filters(filters)));

        // Assert
        var clauses = Dsl.Clauses(json.GetProperty("bool"), "filter");
        Assert.Equal(2, clauses.Count);
        Assert.All(clauses, clause => Assert.True(clause.TryGetProperty("term", out _)));
    }

    [Fact]
    public void Filters_SeveralBoroughs_BuildsADisjunctionThatWidensResults()
    {
        // Arrange
        var filters = new ListingFilters { Boroughs = ["Brooklyn", "Queens"] };

        // Act
        var json = Dsl.Of(Assert.Single(ListingQueryBuilder.Filters(filters)));

        // Assert
        Assert.Equal(2, json.GetProperty("terms").GetProperty("borough").GetArrayLength());
    }

    [Fact]
    public void Filters_ExcludedDimension_OmitsThatDimensionsClause()
    {
        // A borough facet computed with the borough filter applied reports the selected borough
        // and zero for everything else, which is how a filter list turns into a dead end.

        // Arrange
        var filters = new ListingFilters { Boroughs = ["Brooklyn"], Bedrooms = [2] };

        // Act
        var all = ListingQueryBuilder.Filters(filters);
        var withoutBorough = ListingQueryBuilder.Filters(filters, FilterDimension.Borough);

        // Assert
        Assert.Equal(2, all.Count);
        Assert.DoesNotContain("borough", Dsl.Text(Assert.Single(withoutBorough)), StringComparison.Ordinal);
    }

    [Fact]
    public void Filters_SeveralDimensions_BuildsOneClausePerDimension()
    {
        // Arrange
        var filters = new ListingFilters
        {
            MinRent = 1000,
            MaxRent = 5000,
            Bedrooms = [1, 2],
            Boroughs = ["Brooklyn"],
            Amenities = ["Elevator"],
        };

        // Act
        var clauses = ListingQueryBuilder.Filters(filters);

        // Assert -- rent is one clause, not two, because a range carries both bounds.
        Assert.Equal(4, clauses.Count);
    }

    [Fact]
    public void Knn_WithFilters_PushesThemIntoTheVectorLeg()
    {
        // Without them a vector hit can be a $30,000 Manhattan loft answering a search filtered
        // to cheap Brooklyn studios: semantically close, and exactly what the user excluded.

        // Arrange
        var filters = new ListingFilters { Boroughs = ["Brooklyn"], MaxRent = 3000 };

        // Act
        var json = Dsl.Of(ListingQueryBuilder.Knn([0.1f, 0.2f], filters, k: 20, candidates: 100));

        // Assert
        Assert.Equal("descriptionVector", json.GetProperty("field").GetString());
        Assert.Equal(20, json.GetProperty("k").GetInt32());
        Assert.Equal(100, json.GetProperty("num_candidates").GetInt32());
        Assert.Equal(2, Dsl.Clauses(json, "filter").Count);
    }

    [Fact]
    public void Knn_NoFilters_SendsNoFilterClause()
    {
        // Act
        var json = Dsl.Of(ListingQueryBuilder.Knn([0.1f], new ListingFilters(), k: 20, candidates: 100));

        // Assert
        Assert.False(json.TryGetProperty("filter", out _));
    }

    [Fact]
    public void ByIds_PageOfIds_FetchesExactlyThose()
    {
        // Arrange
        string[] ids = ["101", "202", "303"];

        // Act
        var json = Dsl.Of(ListingQueryBuilder.ByIds(ids));

        // Assert
        var terms = json.GetProperty("terms").GetProperty("id").EnumerateArray()
            .Select(term => term.GetString()).ToArray();

        Assert.Equal(ids, terms);
    }

    [Fact]
    public void Sort_Relevance_ExpressesItAsNoSortAtAll()
    {
        // Relevance is Elasticsearch's default, so saying so explicitly would only cost a clause.

        // Act
        var sort = ListingQueryBuilder.Sort(ListingSort.Relevance, near: null);

        // Assert
        Assert.Empty(sort);
    }

    [Theory]
    [InlineData(ListingSort.PriceAsc, "asc")]
    [InlineData(ListingSort.PriceDesc, "desc")]
    public void Sort_ByPrice_OrdersByMonthlyRent(ListingSort sort, string direction)
    {
        // Act
        var json = Dsl.Of(Assert.Single(ListingQueryBuilder.Sort(sort, near: null)));

        // Assert
        Assert.Equal(direction, json.GetProperty("monthlyRent").GetProperty("order").GetString());
    }

    [Fact]
    public void Sort_ByReviewScore_PutsUnreviewedListingsLast()
    {
        // A listing with no score is not a listing with a bad one, but it is not what someone
        // sorting by rating asked to see.

        // Act
        var json = Dsl.Of(Assert.Single(ListingQueryBuilder.Sort(ListingSort.ReviewScoreDesc, near: null)));

        // Assert
        var order = json.GetProperty("reviewScore");
        Assert.Equal("desc", order.GetProperty("order").GetString());
        Assert.Equal("max", order.GetProperty("mode").GetString());
    }

    [Fact]
    public void Sort_ByDistanceWithNoOrigin_FallsBackToRelevance()
    {
        // Asking for it without a centre is a caller error, but a failed search is a poor way
        // to say so.

        // Act
        var sort = ListingQueryBuilder.Sort(ListingSort.DistanceAsc, near: null);

        // Assert
        Assert.Empty(sort);
    }

    [Fact]
    public void Sort_ByDistanceWithAnOrigin_OrdersByDistance()
    {
        // Act
        var json = Dsl.Of(Assert.Single(ListingQueryBuilder.Sort(ListingSort.DistanceAsc, new GeoPoint(40.7, -73.9))));

        // Assert
        Assert.True(json.TryGetProperty("_geo_distance", out _));
    }
}
