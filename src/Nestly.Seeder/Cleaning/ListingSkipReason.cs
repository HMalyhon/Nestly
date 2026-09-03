namespace Nestly.Seeder.Cleaning;

/// <summary>Why a row did not become a listing. Counted and reported, never swallowed.</summary>
internal enum ListingSkipReason
{
    None = 0,
    Identifier,
    Coordinates,
    Price,
    Description,
    Bathrooms,
    Bedrooms,
}
