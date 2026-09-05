using Microsoft.AspNetCore.Mvc;
using Nestly.Api.Validation;
using Nestly.Domain;
using Nestly.Search.Searching;

namespace Nestly.Api.Controllers;

[ApiController]
[Route("api/listings")]
[Produces("application/json")]
public sealed class ListingsMapController(IListingMapService map) : ControllerBase
{
    /// <summary>Map markers for the same search: pins while they fit, density cells when they do not.</summary>
    // Its own controller because the map answers a different question from the result list, and
    // shares only the filters.
    [HttpPost("map", Name = "MapListings")]
    [ProducesResponseType<MapResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MapResponse>> Map(
        [FromBody] ListingMapRequest request,
        CancellationToken cancellationToken)
    {
        if (!ListingMapRequestValidator.TryValidate(request, ModelState))
        {
            return ValidationProblem(ModelState);
        }

        return await map.GetAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
