// Mirrors the contracts in Nestly.Domain. Hand-written rather than generated: the OpenAPI
// document still describes `sort` as an integer, so codegen would produce the wrong type today.

export type ListingSort = 'Relevance' | 'PriceAsc' | 'PriceDesc' | 'ReviewScoreDesc' | 'DistanceAsc';

export type MatchSource = 'None' | 'Lexical' | 'Vector' | 'Both';

export interface GeoPoint {
  lat: number;
  lon: number;
}

export interface GeoBounds {
  topLat: number;
  leftLon: number;
  bottomLat: number;
  rightLon: number;
}

export interface Listing {
  id: string;
  title: string;
  description: string;
  neighborhood: string;
  borough: string;
  location: GeoPoint;
  pricePerNight: number;
  monthlyRent: number;
  bedrooms: number;
  bathrooms: number;
  accommodates: number;
  propertyType: string;
  roomType: string;
  amenities: string[];
  minimumNights: number;

  // Absent, not null: the API drops nulls, so a listing with no reviews has no key at all.
  reviewScore?: number;
  lastReviewedAt?: string;

  // descriptionVector is on the domain record but never on the wire: every endpoint excludes it
  // from _source, and 384 floats are no use to a card.
}

export interface ListingHit {
  listing: Listing;
  score: number;
  highlights: string[];
  matchedBy: MatchSource;
  distanceKm?: number;
}

export interface FacetBucket {
  key: string;
  count: number;
}

export interface ListingFacets {
  boroughs: FacetBucket[];
  neighborhoods: FacetBucket[];
  bedrooms: FacetBucket[];
  roomTypes: FacetBucket[];
  propertyTypes: FacetBucket[];
  amenities: FacetBucket[];
  rentHistogram: FacetBucket[];
  minRent?: number;
  maxRent?: number;
}

export interface ListingFilters {
  minRent?: number;
  maxRent?: number;
  bedrooms?: number[];
  minBathrooms?: number;
  minAccommodates?: number;
  boroughs?: string[];
  neighborhoods?: string[];
  roomTypes?: string[];
  propertyTypes?: string[];
  amenities?: string[];
  minReviewScore?: number;
  near?: GeoPoint;
  radiusKm?: number;
  within?: GeoBounds;
}

export interface ListingSearchRequest {
  query?: string;
  filters?: ListingFilters;
  sort?: ListingSort;
  page?: number;
  pageSize?: number;
}

export interface ListingSearchResponse {
  total: number;
  hits: ListingHit[];
  facets: ListingFacets;
  elapsedMs: number;
}

export type SuggestionKind = 'Neighborhood' | 'Listing';

export interface Suggestion {
  text: string;
  kind: SuggestionKind;
}
