using Microsoft.AspNetCore.Http;
using Nestly.Api.Infrastructure;

namespace Nestly.UnitTests.Infrastructure;

public sealed class ApiRateLimiterTests
{
    /// <summary>What the client's debounces let one browser produce across search and map.</summary>
    private const int ClientCeilingPerSecond = 10;

    [Theory]
    [InlineData("/api/listings/search")]
    [InlineData("/api/listings/map")]
    public void BudgetFor_TheAggregatingEndpoints_ShareOneBudget(string path)
    {
        // Act
        var (group, _, _) = ApiRateLimiter.BudgetFor(new PathString(path));

        // Assert
        Assert.Equal("search", group);
    }

    [Theory]
    [InlineData("/api/listings/search")]
    [InlineData("/api/listings/map")]
    [InlineData("/api/listings/suggest")]
    [InlineData("/api/listings/12345")]
    public void BudgetFor_EveryMeteredPath_SitsWellAboveWhatTheClientCanProduce(string path)
    {
        // Pacing a person is the debounce's job in the browser. These exist to stop a loop, so a
        // limit anywhere near what the front end produces would only be a way to fail a real user.

        // Act
        var (_, burst, perSecond) = ApiRateLimiter.BudgetFor(new PathString(path));

        // Assert
        Assert.True(perSecond >= ClientCeilingPerSecond * 5, $"{path} allows {perSecond}/s sustained.");
        Assert.True(burst >= perSecond * 5, $"{path} allows a burst of only {burst}.");
    }

    [Fact]
    public void BudgetFor_ACheapRead_IsMeteredMoreGenerouslyThanAnAggregation()
    {
        // Act
        var (_, _, read) = ApiRateLimiter.BudgetFor(new PathString("/api/listings/suggest"));
        var (_, _, search) = ApiRateLimiter.BudgetFor(new PathString("/api/listings/search"));

        // Assert
        Assert.True(read > search, $"read {read}/s is not above search {search}/s.");
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/")]
    [InlineData("/openapi/v1.json")]
    public void BudgetFor_AnythingOutsideTheApi_IsNotMetered(string path)
    {
        // Compose gates the web and seeder services on /health, so metering it would let a flood
        // of searches take the stack down by making its own health check fail.

        // Act
        var (_, burst, perSecond) = ApiRateLimiter.BudgetFor(new PathString(path));

        // Assert
        Assert.Equal(0, burst);
        Assert.Equal(0, perSecond);
    }
}
