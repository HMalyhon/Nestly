import type { GeoBounds, ListingFilters, ListingMapRequest, ListingSearchRequest, ListingSort } from '../../api/types';

export const PAGE_SIZE = 20;

/** Mirrors MaxPage in ListingSearchRequestValidator: past it the API returns 400, not an empty page. */
export const MAX_PAGE = 100;

/** Mirrors MaxQueryLength in the same validator. */
export const MAX_QUERY_LENGTH = 200;

/** Mirrors MinZoom/MaxZoom in ListingMapRequestValidator, which are Leaflet's raster range. */
export const MIN_ZOOM = 1;
export const MAX_ZOOM = 20;

/** Mirrors MaxFilterValues and MaxAmenities in ListingSearchRequestValidator. */
const MAX_FILTER_VALUES = 50;
const MAX_AMENITIES = 20;

// The widths the API binds these to, not domain limits: past them System.Text.Json fails to
// convert and the 400 carries a serializer diagnostic rather than a sentence anyone can act on.
const MAX_RENT = 2_147_483_647;
const MAX_BEDROOMS = 255;

/** Manhattan and the inner boroughs at a glance, for a first visit with no bbox in the URL. */
export const DEFAULT_VIEW = { center: [40.7255, -73.955] as [number, number], zoom: 12 };

/**
 * The sorts the UI offers, in order.
 */
// DistanceAsc is deliberately absent: the API rejects it unless Filters.Near is set, so it can
// only appear once the map supplies a centre.
export const SORT_OPTIONS: { value: ListingSort; label: string }[] = [
  { value: 'Relevance', label: 'Best match' },
  { value: 'PriceAsc', label: 'Price: low to high' },
  { value: 'PriceDesc', label: 'Price: high to low' },
  { value: 'ReviewScoreDesc', label: 'Top rated' },
];

/**
 * The URL name of each list filter, and the ListingFilters field it fills.
 */
// One table rather than a parse function and a write function that have to agree: adding a facet
// is a line here, and the two directions cannot drift apart.
export const LIST_FILTERS = {
  borough: 'boroughs',
  hood: 'neighborhoods',
  room: 'roomTypes',
  type: 'propertyTypes',
  amenity: 'amenities',
} as const satisfies Record<string, keyof ListingFilters>;

export type ListFilterParam = keyof typeof LIST_FILTERS;

export interface SearchState {
  query: string;
  sort: ListingSort;
  page: number;
  filters: ListingFilters;

  /** Drives the grid cell size when the map clusters; not a filter. */
  zoom: number;
}

export const DEFAULT_SEARCH: SearchState = {
  query: '',
  sort: 'Relevance',
  page: 1,
  filters: {},
  zoom: DEFAULT_VIEW.zoom,
};

// Leaflet's own toBBoxString order, so the value round-trips through the map without reordering.
const BBOX_PARTS = 4;

function isLatitude(value: number): boolean {
  return value >= -90 && value <= 90;
}

function isLongitude(value: number): boolean {
  return value >= -180 && value <= 180;
}

function readBounds(value: string | null): GeoBounds | undefined {
  const parts = value?.split(',').map(Number) ?? [];

  if (parts.length !== BBOX_PARTS || parts.some((part) => !Number.isFinite(part))) {
    return undefined;
  }

  const [west, south, east, north] = parts as [number, number, number, number];

  // The checks ListingSearchRequestValidator makes, so a hand-edited bbox falls back to no
  // viewport instead of 400ing every request the link makes.
  if (north < south || !isLatitude(north) || !isLatitude(south) || !isLongitude(west) || !isLongitude(east)) {
    return undefined;
  }

  // A box with no area matches nothing, and ?bbox=,,, parses into exactly that at (0, 0).
  if (north === south || west === east) {
    return undefined;
  }

  return { topLat: north, leftLon: west, bottomLat: south, rightLon: east };
}

export function writeBounds(bounds: GeoBounds): string {
  return [bounds.leftLon, bounds.bottomLat, bounds.rightLon, bounds.topLat]
    .map((part) => part.toFixed(5))
    .join(',');
}

function readSort(value: string | null): ListingSort {
  return SORT_OPTIONS.find((option) => option.value === value)?.value ?? DEFAULT_SEARCH.sort;
}

/** A whole number, or undefined when the parameter is not one. */
// Digits only. Number() also reads hex, whitespace and exponent forms, so ?minRent=0x10 became a
// $16 floor and ?minRent=1000.5 reached an int on the API, which rejects it rather than rounding.
function readNumber(value: string | null): number | undefined {
  return value !== null && /^\d+$/.test(value) && Number(value) <= Number.MAX_SAFE_INTEGER
    ? Number(value)
    : undefined;
}

/** The same, dropping anything too wide for the field the API binds it to. */
// Dropped rather than clamped: a rent silently rewritten to two billion is not what was asked for.
function readBounded(value: string | null, max: number): number | undefined {
  const parsed = readNumber(value);

  return parsed !== undefined && parsed <= max ? parsed : undefined;
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

function readPage(value: string | null): number {
  const page = readNumber(value);

  // Clamped, not just validated: a hand-edited ?page=250 should show the last page the API will
  // serve rather than send a request it is certain to reject.
  return page === undefined ? DEFAULT_SEARCH.page : clamp(page, 1, MAX_PAGE);
}

function readZoom(value: string | null): number {
  // Not `Number(z) || DEFAULT`: zoom 0 is falsy, so a map zoomed all the way out read back as 12
  // and the URL disagreed with the grid it was describing.
  const zoom = readNumber(value);

  return zoom === undefined ? DEFAULT_VIEW.zoom : clamp(zoom, MIN_ZOOM, MAX_ZOOM);
}

function readFilters(params: URLSearchParams): ListingFilters {
  const filters: ListingFilters = {};

  for (const [param, field] of Object.entries(LIST_FILTERS)) {
    // Repeated parameters rather than one comma-joined value: neighborhood and amenity names are
    // free text, and URLSearchParams already round-trips repeats without an escaping scheme.
    const values = params
      .getAll(param)
      .filter((value) => value !== '')
      .slice(0, param === 'amenity' ? MAX_AMENITIES : MAX_FILTER_VALUES);

    if (values.length > 0) {
      Object.assign(filters, { [field]: values });
    }
  }

  // Number('') is 0, which passed every guard here and turned a truncated ?beds= into a studio
  // filter nobody asked for. readNumber rejects it with the rest of the coercions.
  const bedrooms = params
    .getAll('beds')
    .flatMap((value) => readBounded(value, MAX_BEDROOMS) ?? [])
    .slice(0, MAX_FILTER_VALUES);

  if (bedrooms.length > 0) {
    filters.bedrooms = bedrooms;
  }

  const within = readBounds(params.get('bbox'));

  if (within) {
    filters.within = within;
  }

  const minRent = readBounded(params.get('minRent'), MAX_RENT);
  const maxRent = readBounded(params.get('maxRent'), MAX_RENT);

  // An inverted range is not a range: the API rejects it, and there is no way to tell which of the
  // two the reader meant, so neither is kept.
  if (minRent !== undefined && maxRent !== undefined && minRent > maxRent) {
    return filters;
  }

  if (minRent !== undefined) {
    filters.minRent = minRent;
  }

  if (maxRent !== undefined) {
    filters.maxRent = maxRent;
  }

  return filters;
}

/** Reads state from the URL, ignoring anything it does not recognise. */
export function readSearchState(params: URLSearchParams): SearchState {
  return {
    query: params.get('q')?.slice(0, MAX_QUERY_LENGTH) ?? DEFAULT_SEARCH.query,
    sort: readSort(params.get('sort')),
    page: readPage(params.get('page')),
    filters: readFilters(params),
    zoom: readZoom(params.get('z')),
  };
}

/** Every parameter this module owns, so a write can clear them without touching the rest. */
const OWNED_PARAMS = ['q', 'sort', 'page', 'beds', 'minRent', 'maxRent', 'bbox', 'z',
  ...Object.keys(LIST_FILTERS)] as const;

/** Only non-default values are written, so a plain search stays a clean link. */
// Seeded from the current URL rather than empty: campaign and referral parameters are nobody
// else's to delete, and building fresh dropped them on the first keystroke.
export function writeSearchState(state: SearchState, current?: URLSearchParams): URLSearchParams {
  const params = new URLSearchParams(current);

  for (const param of OWNED_PARAMS) {
    params.delete(param);
  }

  if (state.query) {
    params.set('q', state.query);
  }

  if (state.sort !== DEFAULT_SEARCH.sort) {
    params.set('sort', state.sort);
  }

  for (const [param, field] of Object.entries(LIST_FILTERS)) {
    for (const value of state.filters[field] ?? []) {
      params.append(param, value);
    }
  }

  for (const bedrooms of state.filters.bedrooms ?? []) {
    params.append('beds', String(bedrooms));
  }

  if (state.filters.minRent !== undefined) {
    params.set('minRent', String(state.filters.minRent));
  }

  if (state.filters.maxRent !== undefined) {
    params.set('maxRent', String(state.filters.maxRent));
  }

  // Where the map is looking, not something the user chose from a list -- so it survives "clear"
  // and does not count towards the active-filter badge.
  if (state.filters.within) {
    params.set('bbox', writeBounds(state.filters.within));
    params.set('z', String(state.zoom));
  }

  // Last, so the interesting part of a shared link is the part people read.
  if (state.page !== DEFAULT_SEARCH.page) {
    params.set('page', String(state.page));
  }

  return params;
}

/** How many filter values are active, for the "clear" control and the mobile badge. */
export function countFilters(filters: ListingFilters): number {
  const lists = Object.values(LIST_FILTERS).reduce((total, field) => total + (filters[field]?.length ?? 0), 0);
  const rent = (filters.minRent === undefined ? 0 : 1) + (filters.maxRent === undefined ? 0 : 1);

  return lists + (filters.bedrooms?.length ?? 0) + rent;
}

/** How many pages the UI may offer for a result set, within what the API will serve. */
export function pageCount(total: number): number {
  return Math.min(Math.ceil(total / PAGE_SIZE), MAX_PAGE);
}

export function toMapRequest(state: SearchState): ListingMapRequest {
  return { query: state.query, filters: state.filters, zoom: state.zoom };
}

export function toRequest(state: SearchState): ListingSearchRequest {
  return {
    query: state.query,
    filters: state.filters,
    sort: state.sort,
    page: state.page,
    pageSize: PAGE_SIZE,
  };
}
