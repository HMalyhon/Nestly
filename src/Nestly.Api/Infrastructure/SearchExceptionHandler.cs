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
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

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
