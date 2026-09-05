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

    // Every token in the query becomes a fuzzy clause across three fields, so cost is linear in
    // length: 500 words takes 18 seconds, 1,000 times out. This is a search box, not an essay.
    private const int MaxQueryLength = 200;

    // The seeder normalises amenities onto 20 canonical values, and each one is a separate
    // clause; the rest are keyword filters where a long list is meaningless too.
    private const int MaxAmenities = 20;
    private const int MaxFilterValues = 50;

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

        ValidateSearch(request.Query, request.Filters, modelState);

        var filters = request.Filters;

        if (request.Sort == ListingSort.DistanceAsc && filters.Near is null)
        {
            Fail(nameof(request.Sort), "Sorting by distance requires Filters.Near.");
        }

        return modelState.IsValid;
    }

    /// <summary>The rules the search and the map share: free text and filters.</summary>
    public static void ValidateSearch(string? query, ListingFilters filters, ModelStateDictionary modelState)
    {
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(modelState);

        void Fail(string field, string message) => modelState.AddModelError(field, message);

        if (query?.Length > MaxQueryLength)
        {
            Fail("Query", $"Query must be {MaxQueryLength} characters or fewer.");
        }

        CheckCount(nameof(filters.Amenities), filters.Amenities.Count, MaxAmenities);
        CheckCount(nameof(filters.Neighborhoods), filters.Neighborhoods.Count, MaxFilterValues);
        CheckCount(nameof(filters.Boroughs), filters.Boroughs.Count, MaxFilterValues);
        CheckCount(nameof(filters.RoomTypes), filters.RoomTypes.Count, MaxFilterValues);
        CheckCount(nameof(filters.PropertyTypes), filters.PropertyTypes.Count, MaxFilterValues);
        CheckCount(nameof(filters.Bedrooms), filters.Bedrooms.Count, MaxFilterValues);

        if (filters.MinRent is < 0)
        {
            Fail(nameof(filters.MinRent), "MinRent cannot be negative.");
        }

        if (filters.MaxRent is < 0)
        {
            Fail(nameof(filters.MaxRent), "MaxRent cannot be negative.");
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

        if (filters.Near is { } near && (!IsLatitude(near.Lat) || !IsLongitude(near.Lon)))
        {
            Fail(nameof(filters.Near), "Near must be a valid latitude and longitude.");
        }

        if (filters.Within is { } bounds)
        {
            if (bounds.TopLat < bounds.BottomLat)
            {
                Fail(nameof(filters.Within), "Within.TopLat must be north of Within.BottomLat.");
            }

            // Unchecked, a viewport clamped past a pole reaches Elasticsearch and fails there.
            if (!IsLatitude(bounds.TopLat) || !IsLatitude(bounds.BottomLat) ||
                !IsLongitude(bounds.LeftLon) || !IsLongitude(bounds.RightLon))
            {
                Fail(nameof(filters.Within), "Within must be valid latitudes and longitudes.");
            }
        }

        void CheckCount(string field, int count, int max)
        {
            if (count > max)
            {
                Fail(field, $"{field} accepts at most {max} values.");
            }
        }
    }

    private static bool IsLatitude(double value) => value is >= -90 and <= 90;

    private static bool IsLongitude(double value) => value is >= -180 and <= 180;
}
