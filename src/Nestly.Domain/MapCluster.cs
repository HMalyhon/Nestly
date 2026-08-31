namespace Nestly.Domain;

/// <summary>A <c>geotile_grid</c> bucket: a cell centroid and how many listings fall in it.</summary>
public readonly record struct MapCluster(double Lat, double Lon, long Count, int MedianRent);
