using Nestly.Domain;
using Nestly.Search.Ranking;

namespace Nestly.UnitTests.Ranking;

public sealed class RrfFusionTests
{
    [Fact]
    public void Fuse_DocumentFoundByBothLegs_RanksAboveEitherLegsFavourite()
    {
        // Arrange -- "both" is second in each list and first in neither, which is the whole point
        // of RRF: agreement outranks a single leg's favourite.
        string[] lexical = ["lexical-only", "both"];
        string[] vector = ["vector-only", "both"];

        // Act
        var fused = RrfFusion.Fuse(lexical, vector);

        // Assert
        Assert.Equal("both", fused[0].Id);
        Assert.Equal(MatchSource.Both, fused[0].MatchedBy);
    }

    [Fact]
    public void Fuse_SingleLeg_ScoresByReciprocalRank()
    {
        // Arrange
        string[] lexical = ["a", "b"];

        // Act
        var fused = RrfFusion.Fuse(lexical, []);

        // Assert
        Assert.Equal(1d / (RrfFusion.DefaultK + 1), fused[0].Score, 12);
        Assert.Equal(1d / (RrfFusion.DefaultK + 2), fused[1].Score, 12);
    }

    [Fact]
    public void Fuse_DocumentInBothLegs_AddsEachLegsContribution()
    {
        // Arrange
        string[] shared = ["shared"];

        // Act
        var fused = RrfFusion.Fuse(shared, shared);

        // Assert
        Assert.Equal(2d / (RrfFusion.DefaultK + 1), Assert.Single(fused).Score, 12);
    }

    [Fact]
    public void Fuse_MixedResults_MarksWhichLegFoundEachDocument()
    {
        // Arrange
        string[] lexical = ["lex", "both"];
        string[] vector = ["vec", "both"];

        // Act
        var matchedBy = RrfFusion.Fuse(lexical, vector).ToDictionary(hit => hit.Id, hit => hit.MatchedBy);

        // Assert
        Assert.Equal(MatchSource.Lexical, matchedBy["lex"]);
        Assert.Equal(MatchSource.Vector, matchedBy["vec"]);
        Assert.Equal(MatchSource.Both, matchedBy["both"]);
    }

    [Fact]
    public void Fuse_EqualScores_BreaksTheTieByFirstAppearance()
    {
        // Arrange -- rank 1 of one list against rank 1 of the other: identical scores, and
        // without a deterministic tiebreak a page could reorder between two identical requests.
        string[] lexical = ["lex"];
        string[] vector = ["vec"];

        // Act
        var fused = RrfFusion.Fuse(lexical, vector);

        // Assert
        Assert.Equal(fused[0].Score, fused[1].Score, 12);
        Assert.Equal(["lex", "vec"], fused.Select(hit => hit.Id));
    }

    [Fact]
    public void Fuse_OneEmptyLeg_KeepsTheOtherLegsOrder()
    {
        // Arrange -- the degenerate case the browse path relies on.
        IReadOnlyList<string> lexical = ["a", "b", "c"];

        // Act
        var fused = RrfFusion.Fuse(lexical, []);

        // Assert -- fusion must not reorder what lexical ranking already decided.
        Assert.Equal(lexical, fused.Select(hit => hit.Id));
    }

    [Fact]
    public void Fuse_BothLegsEmpty_ReturnsNothing()
    {
        // Act
        var fused = RrfFusion.Fuse([], []);

        // Assert
        Assert.Empty(fused);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(1000)]
    public void Fuse_AnyK_RanksAgreementFirst(int k)
    {
        // Arrange -- k controls how quickly rank stops mattering, never whether agreement counts.
        string[] lexical = ["lexical-only", "both"];
        string[] vector = ["vector-only", "both"];

        // Act
        var fused = RrfFusion.Fuse(lexical, vector, k);

        // Assert
        Assert.Equal("both", fused[0].Id);
    }

    [Fact]
    public void Fuse_SmallK_LetsATopRankOutweighAgreementFurtherDown()
    {
        // Arrange -- the sensitivity that justifies k=60. At k=1 first place scores 1/2 while a
        // document both legs put fourth scores 2/5, so a single leg's favourite wins; larger k
        // flattens the curve until agreement takes over. The filler ids differ between the legs
        // on purpose, since shared ones would be agreed upon too and outscore both.
        string[] lexical = ["lexical-first", "lex-a", "lex-b", "agreed"];
        string[] vector = ["vector-first", "vec-a", "vec-b", "agreed"];

        // Act
        var atOne = RrfFusion.Fuse(lexical, vector, k: 1);
        var atSixty = RrfFusion.Fuse(lexical, vector, RrfFusion.DefaultK);

        // Assert
        Assert.Equal("lexical-first", atOne[0].Id);
        Assert.Equal("agreed", atSixty[0].Id);
    }

    [Fact]
    public void Fuse_DocumentInBothLegs_ReturnsItOnce()
    {
        // Arrange
        string[] lexical = ["a", "b"];
        string[] vector = ["b", "a"];

        // Act
        var fused = RrfFusion.Fuse(lexical, vector);

        // Assert
        Assert.Equal(2, fused.Count);
        Assert.Equal(2, fused.Select(hit => hit.Id).Distinct(StringComparer.Ordinal).Count());
    }
}
