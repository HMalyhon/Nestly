using Microsoft.AspNetCore.Mvc;
using Nestly.Api.Validation;
using Nestly.Domain;
using Nestly.Search.Searching;

namespace Nestly.Api.Controllers;

[ApiController]
[Route("api/listings")]
[Produces("application/json")]
public sealed class ListingsController(IListingSearchService search) : ControllerBase
{
    /// <summary>Search listings by free text and filters.</summary>
    // POST, though it reads: the request nests filter objects and arrays that a query string
    // handles badly. Shareable search URLs are the front end's job.
    [HttpPost("search", Name = "SearchListings")]
    [ProducesResponseType<ListingSearchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ListingSearchResponse>> Search(
        [FromBody] ListingSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!ListingSearchRequestValidator.TryValidate(request, ModelState))
        {
            return ValidationProblem(ModelState);
        }

        return await search.SearchAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
