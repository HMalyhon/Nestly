using Microsoft.AspNetCore.Mvc;
using Nestly.Domain;
using Nestly.Search.Searching;

namespace Nestly.Api.Controllers;

[ApiController]
[Route("api/listings")]
[Produces("application/json")]
public sealed class ListingsSuggestController(IListingSuggestService suggest) : ControllerBase
{
    private const int MaxQueryLength = 100;

    /// <summary>Autocomplete: neighborhoods first, then listing titles.</summary>
    [HttpGet("suggest", Name = "SuggestListings")]
    [ProducesResponseType<IReadOnlyList<Suggestion>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<Suggestion>>> Suggest(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        // Nullable so that backspacing the box empty is an empty list rather than a 400.
        if (q?.Length > MaxQueryLength)
        {
            ModelState.AddModelError(nameof(q), $"q must be {MaxQueryLength} characters or fewer.");

            return ValidationProblem(ModelState);
        }

        // GET with a single scalar, unlike search and map: this one is a keystroke, and it should
        // be as cacheable and as cheap to fire as the browser can make it.
        return Ok(await suggest.SuggestAsync(q ?? string.Empty, cancellationToken).ConfigureAwait(false));
    }
}
