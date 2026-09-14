using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Nestly.Api.Infrastructure;
using Nestly.Search.Searching;

namespace Nestly.UnitTests.Infrastructure;

public sealed class SearchExceptionHandlerTests
{
    private const int ClientClosedRequest = 499;

    [Fact]
    public async Task TryHandle_RequestTheCallerAbandoned_Answers499WithoutABody()
    {
        // Search-as-you-type supersedes its own requests, so this used to be a 500 and an
        // error-level log per keystroke.

        // Arrange
        var (handler, context) = Handler();
        using var aborted = new CancellationTokenSource();

        await aborted.CancelAsync();
        context.RequestAborted = aborted.Token;

        // Act
        var handled = await handler.TryHandleAsync(context, new OperationCanceledException(), CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(ClientClosedRequest, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task TryHandle_CancellationTheCallerDidNotCause_IsNotTreatedAsAnAbandonedRequest()
    {
        // Arrange -- RequestAborted is untouched, so this is a timeout or a bug, not a keystroke.
        var (handler, context) = Handler();

        // Act
        var handled = await handler.TryHandleAsync(context, new OperationCanceledException(), CancellationToken.None);

        // Assert
        Assert.False(handled);
    }

    [Theory]
    [InlineData(true, StatusCodes.Status400BadRequest)]
    [InlineData(false, StatusCodes.Status503ServiceUnavailable)]
    public async Task TryHandle_SearchFailure_ReportsWhoseFaultItWas(bool causedByRequest, int expected)
    {
        // Arrange
        var (handler, context) = Handler();

        // Act
        await handler.TryHandleAsync(context, new SearchException("no", causedByRequest), CancellationToken.None);

        // Assert
        Assert.Equal(expected, context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandle_SomethingElseEntirely_LeavesItToTheFramework()
    {
        // Arrange
        var (handler, context) = Handler();

        // Act
        var handled = await handler.TryHandleAsync(context, new InvalidOperationException(), CancellationToken.None);

        // Assert
        Assert.False(handled);
    }

    private static (SearchExceptionHandler Handler, DefaultHttpContext Context) Handler()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        return (new SearchExceptionHandler(new StubProblemDetails()), context);
    }

    private sealed class StubProblemDetails : IProblemDetailsService
    {
        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context) => ValueTask.FromResult(true);

        public ValueTask WriteAsync(ProblemDetailsContext context) => ValueTask.CompletedTask;
    }
}
