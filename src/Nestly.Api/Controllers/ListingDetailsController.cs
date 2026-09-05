using Microsoft.AspNetCore.Mvc;
using Nestly.Domain;
using Nestly.Search.Searching;

namespace Nestly.Api.Controllers;

[ApiController]
[Route("api/listings")]
[Produces("application/json")]
public sealed class ListingDetailsController(IListingDetailService listings) : ControllerBase
{
    /// <summary>Fetch one listing by its source identifier.</summary>
    // Constrained to :long, and bound as the string the domain actually stores. Unconstrained,
    // "{id}" also answers GET /api/listings/map -- a sibling route that only accepts POST -- and
    // the caller gets "no listing has the id 'map'" instead of a routing failure.
    [HttpGet("{id:long}", Name = "GetListing")]
    [ProducesResponseType<Listing>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Listing>> Get(string id, CancellationToken cancellationToken)
    {
        var listing = await listings.GetAsync(id, cancellationToken).ConfigureAwait(false);

        return listing is null
            ? Problem(
                title: "Listing not found.",
                detail: $"No listing has the id '{id}'.",
                statusCode: StatusCodes.Status404NotFound)
            : Ok(listing);
    }
}
