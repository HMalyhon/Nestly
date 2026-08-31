namespace Nestly.Domain;

/// <summary>A WGS84 coordinate pair, mapped to an Elasticsearch <c>geo_point</c>.</summary>
public readonly record struct GeoPoint(double Lat, double Lon);
