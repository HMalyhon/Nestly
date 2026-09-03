using System.Globalization;
using Nestly.Domain;
using Nestly.Seeder.Csv;

namespace Nestly.Seeder.Cleaning;

/// <summary>
/// Turns a published CSV row into the <see cref="Listing"/> the index stores.
/// </summary>
/// <remarks>
/// This is the only place in the project that invents a value, and it invents exactly one:
/// <see cref="Listing.MonthlyRent"/>. Everything else is parsed from the source or the row is
/// dropped. See data/README.md.
/// </remarks>
internal static class ListingMapper
{
    /// <summary>
    /// Nightly rates are what the source publishes; Nestly presents monthly rents. Thirty is a
    /// plain multiplier rather than a tuned one: a smaller factor would produce prettier numbers,
    /// which is exactly what would make it a fudge.
    /// </summary>
    private const int NightsPerMonth = 30;

    public static bool TryMap(ListingCsvRow row, out Listing listing, out ListingSkipReason reason)
    {
        ArgumentNullException.ThrowIfNull(row);

        listing = null!;

        if (string.IsNullOrWhiteSpace(row.Id))
        {
            reason = ListingSkipReason.Identifier;
            return false;
        }

        if (!TryParseDouble(row.Latitude, out var latitude) || !TryParseDouble(row.Longitude, out var longitude))
        {
            reason = ListingSkipReason.Coordinates;
            return false;
        }

        if (!TryParsePrice(row.Price, out var pricePerNight) || pricePerNight <= 0)
        {
            reason = ListingSkipReason.Price;
            return false;
        }

        // Cleaned first, then checked: a description of nothing but markup is as useless to
        // search as an empty one, and both the lexical and the vector leg would index noise.
        var description = HtmlText.Clean(row.Description);

        if (description.Length == 0)
        {
            reason = ListingSkipReason.Description;
            return false;
        }

        if (!TryParseBathrooms(row.BathroomsText, out var bathrooms))
        {
            reason = ListingSkipReason.Bathrooms;
            return false;
        }

        if (!TryParseByte(row.Bedrooms, out var bedrooms))
        {
            reason = ListingSkipReason.Bedrooms;
            return false;
        }

        listing = new Listing
        {
            Id = row.Id.Trim(),
            Title = HtmlText.Clean(row.Name),
            Description = description,
            Neighborhood = row.Neighborhood.Trim(),
            Borough = row.Borough.Trim(),
            Location = new GeoPoint(latitude, longitude),
            PricePerNight = pricePerNight,
            MonthlyRent = pricePerNight * NightsPerMonth,
            Bedrooms = bedrooms,
            Bathrooms = bathrooms,
            Accommodates = TryParseByte(row.Accommodates, out var accommodates) ? accommodates : (byte)0,
            PropertyType = row.PropertyType.Trim(),
            RoomType = row.RoomType.Trim(),
            Amenities = AmenityCatalog.Normalize(row.Amenities),

            // The optional three. A listing with no reviews yet is a real listing, so an absent
            // score means "unrated", not "malformed" -- it is left null and the facet ignores it.
            ReviewScore = TryParseDouble(row.ReviewScore, out var score) ? score : null,
            MinimumNights = TryParseShort(row.MinimumNights, out var minimumNights) ? minimumNights : (short)1,
            LastReviewedAt = ParseDate(row.LastReview),
        };

        reason = ListingSkipReason.None;
        return true;
    }

    /// <summary>Parses a price literal such as <c>"$1,250.00"</c> to whole dollars.</summary>
    internal static bool TryParsePrice(string? raw, out int price)
    {
        price = 0;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var cleaned = raw.Trim().TrimStart('$').Replace(",", string.Empty, StringComparison.Ordinal);

        if (!decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        price = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return true;
    }

    /// <summary>
    /// Parses the free-text bathroom column: "1 bath", "2.5 baths", "1 shared bath",
    /// "Half-bath", "Private half-bath". Shared and private are dropped -- the count is what a
    /// filter can use, and "shared" is already visible in the room type.
    /// </summary>
    internal static bool TryParseBathrooms(string? raw, out decimal bathrooms)
    {
        bathrooms = 0m;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var text = raw.Trim().ToLowerInvariant();

        // "Half-bath" carries no digit at all, so the numeric parse below would reject a listing
        // that does have a bathroom, just half of one.
        if (!char.IsAsciiDigit(text[0]))
        {
            if (text.Contains("half", StringComparison.Ordinal))
            {
                bathrooms = 0.5m;
                return true;
            }

            return false;
        }

        var space = text.IndexOf(' ', StringComparison.Ordinal);
        var count = space > 0 ? text.AsSpan(0, space) : text.AsSpan();

        return decimal.TryParse(count, NumberStyles.Number, CultureInfo.InvariantCulture, out bathrooms);
    }

    private static bool TryParseDouble(string? raw, out double value) =>
        double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static bool TryParseByte(string? raw, out byte value)
    {
        value = 0;

        // Written as decimals upstream ("1.0"), and a bedroom count is never fractional.
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        var rounded = Math.Round(parsed, MidpointRounding.AwayFromZero);
        value = rounded is >= 0 and <= byte.MaxValue ? (byte)rounded : byte.MaxValue;
        return true;
    }

    private static bool TryParseShort(string? raw, out short value)
    {
        value = 0;

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        // Some hosts write a minimum stay of 1,125 nights; a few write more than a short holds.
        value = (short)Math.Clamp(parsed, short.MinValue, short.MaxValue);
        return true;
    }

    private static DateOnly? ParseDate(string? raw) =>
        DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null;
}
