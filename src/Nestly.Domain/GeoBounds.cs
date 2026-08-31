namespace Nestly.Domain;

/// <summary>A map viewport, applied as an Elasticsearch <c>geo_bounding_box</c>.</summary>
public readonly record struct GeoBounds(double TopLat, double LeftLon, double BottomLat, double RightLon);
