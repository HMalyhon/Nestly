using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Domain;

namespace Nestly.Api.Validation;

/// <summary>Validates a map request: the search rules, plus a sane zoom.</summary>
internal static class ListingMapRequestValidator
{
    // Leaflet's own range for raster tiles. Zoom sizes the grid cells when results cluster.
    private const int MinZoom = 1;
    private const int MaxZoom = 20;

    public static bool TryValidate(ListingMapRequest request, ModelStateDictionary modelState)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(modelState);

        ListingSearchRequestValidator.ValidateSearch(request.Query, request.Filters, modelState);

        if (request.Zoom is < MinZoom or > MaxZoom)
        {
            modelState.AddModelError(nameof(request.Zoom), $"Zoom must be between {MinZoom} and {MaxZoom}.");
        }

        return modelState.IsValid;
    }
}
