import type { ListingFilters, ListingSearchRequest, ListingSort } from '../../api/types';

export const PAGE_SIZE = 20;

/** Mirrors MaxPage in ListingSearchRequestValidator: past it the API returns 400, not an empty page. */
export const MAX_PAGE = 100;

/** Mirrors MaxQueryLength in the same validator. */
export const MAX_QUERY_LENGTH = 200;

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
}

export const DEFAULT_SEARCH: SearchState = { query: '', sort: 'Relevance', page: 1, filters: {} };

function readSort(value: string | null): ListingSort {
  return SORT_OPTIONS.find((option) => option.value === value)?.value ?? DEFAULT_SEARCH.sort;
}

function readPage(value: string | null): number {
  const page = Number(value);

  // Clamped, not just validated: a hand-edited ?page=250 should show the last page the API will
  // serve rather than send a request it is certain to reject.
  return Number.isInteger(page) ? Math.min(Math.max(page, 1), MAX_PAGE) : DEFAULT_SEARCH.page;
}

function readNumber(value: string | null): number | undefined {
  const parsed = Number(value);

  return value !== null && value !== '' && Number.isFinite(parsed) && parsed >= 0 ? parsed : undefined;
}

function readFilters(params: URLSearchParams): ListingFilters {
  const filters: ListingFilters = {};

  for (const [param, field] of Object.entries(LIST_FILTERS)) {
    // Repeated parameters rather than one comma-joined value: neighborhood and amenity names are
    // free text, and URLSearchParams already round-trips repeats without an escaping scheme.
    const values = params.getAll(param);

    if (values.length > 0) {
      Object.assign(filters, { [field]: values });
    }
  }

  const bedrooms = params.getAll('beds').map(Number).filter((value) => Number.isInteger(value) && value >= 0);

  if (bedrooms.length > 0) {
    filters.bedrooms = bedrooms;
  }

  const minRent = readNumber(params.get('minRent'));
  const maxRent = readNumber(params.get('maxRent'));

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
  };
}

/** Only non-default values are written, so a plain search stays a clean link. */
export function writeSearchState(state: SearchState): URLSearchParams {
  const params = new URLSearchParams();

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

export function toRequest(state: SearchState): ListingSearchRequest {
  return {
    query: state.query,
    filters: state.filters,
    sort: state.sort,
    page: state.page,
    pageSize: PAGE_SIZE,
  };
}
