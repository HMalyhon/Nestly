using Nestly.Domain;

namespace Nestly.Search.Searching;

/// <summary>Great-circle distance between two coordinates, in kilometres.</summary>
// Haversine on a spherical earth: metres of error across a city, which a "1.2 km away" label
// will never notice.
public static class GeoDistance
{
    private const double EarthRadiusKm = 6371.0088;

    public static double Kilometers(GeoPoint from, GeoPoint to)
    {
        var latitudeDelta = ToRadians(to.Lat - from.Lat);
        var longitudeDelta = ToRadians(to.Lon - from.Lon);

        var a = (Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2)) +
                (Math.Cos(ToRadians(from.Lat)) * Math.Cos(ToRadians(to.Lat)) *
                 Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2));

        return EarthRadiusKm * 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
