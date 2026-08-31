namespace Nestly.Domain;

/// <summary>A single map marker, trimmed to what the map actually draws.</summary>
public readonly record struct MapPin(string Id, double Lat, double Lon, int MonthlyRent);
