using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nestly.Domain;

namespace Nestly.Api.Validation;

/// <summary>Validates a search request, reporting every problem at once.</summary>
// Hand-written because the rules that matter span two fields -- a radius without a centre, a
// maximum below a minimum -- which data annotations express poorly.
public static class ListingSearchRequestValidator
{
    // Caps, so one request cannot ask the cluster for a data export or page 500 deep.
    private const int MaxPageSize = 100;
    private const int MaxPage = 100;

    private const double MaxRadiusKm = 50;

    /// <summary>Adds a message per broken rule, and returns whether the request is usable.</summary>
    // Writes into ModelState rather than returning its own shape, so these 400s come out of the
    // same ProblemDetails factory as the framework's own -- same type, same traceId.
    public static bool TryValidate(ListingSearchRequest request, ModelStateDictionary modelState)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(modelState);

        void Fail(string field, string message) => modelState.AddModelError(field, message);

        if (request.Page is < 1 or > MaxPage)
        {
            Fail(nameof(request.Page), $"Page must be between 1 and {MaxPage}.");
        }

        if (request.PageSize is < 1 or > MaxPageSize)
        {
            Fail(nameof(request.PageSize), $"PageSize must be between 1 and {MaxPageSize}.");
        }

        var filters = request.Filters;

        if (filters.MinRent is < 0)
        {
            Fail(nameof(filters.MinRent), "MinRent cannot be negative.");
        }

        if (filters.MinRent is { } min && filters.MaxRent is { } max && min > max)
        {
            Fail(nameof(filters.MaxRent), "MaxRent must be greater than or equal to MinRent.");
        }

        if (filters.MinReviewScore is < 0 or > 5)
        {
            Fail(nameof(filters.MinReviewScore), "MinReviewScore must be between 0 and 5.");
        }

        // A radius without a centre is a caller bug, not a default to guess at.
        if (filters.RadiusKm is not null && filters.Near is null)
        {
            Fail(nameof(filters.Near), "Near is required when RadiusKm is set.");
        }

        if (filters.RadiusKm is <= 0 or > MaxRadiusKm)
        {
            Fail(nameof(filters.RadiusKm), $"RadiusKm must be between 0 and {MaxRadiusKm}.");
        }

        if (filters.Near is { } near && (near.Lat is < -90 or > 90 || near.Lon is < -180 or > 180))
        {
            Fail(nameof(filters.Near), "Near must be a valid latitude and longitude.");
        }

        if (filters.Within is { } bounds && (bounds.TopLat < bounds.BottomLat))
        {
            Fail(nameof(filters.Within), "Within.TopLat must be north of Within.BottomLat.");
        }

        if (request.Sort == ListingSort.DistanceAsc && filters.Near is null)
        {
            Fail(nameof(request.Sort), "Sorting by distance requires Filters.Near.");
        }

        return modelState.IsValid;
    }
}
