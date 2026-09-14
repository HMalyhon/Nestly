using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Nestly.Search.Searching;

namespace Nestly.Api.Infrastructure;

/// <summary>Turns a failed search into the status code that describes whose fault it was.</summary>
// Without this every Elasticsearch failure is a 500, so a caller cannot tell a malformed request
// from a cluster that is down -- and /health already distinguishes the two.
internal sealed class SearchExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    /// <summary>nginx's 499. Not in StatusCodes, and never reaches the client that caused it.</summary>
    private const int ClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // A keystroke supersedes the request before it: nothing failed, and there is nobody left
        // to send a body to.
        if (httpContext.RequestAborted.IsCancellationRequested && exception is OperationCanceledException)
        {
            httpContext.Response.StatusCode = ClientClosedRequest;

            return true;
        }

        if (exception is not SearchException search)
        {
            return false;
        }

        httpContext.Response.StatusCode = search.CausedByRequest
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status503ServiceUnavailable;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = search,
            ProblemDetails =
            {
                Title = search.CausedByRequest ? "The search request was rejected." : "Search is unavailable.",
                Detail = search.CausedByRequest
                    ? "Elasticsearch could not run this query. Check the filter values."
                    : "The search cluster could not be reached.",
                Status = httpContext.Response.StatusCode,
            },
        }).ConfigureAwait(false);
    }
}
