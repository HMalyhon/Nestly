import type { ListingSearchRequest, ListingSort } from '../../api/types';

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

export interface SearchState {
  query: string;
  sort: ListingSort;
  page: number;
}

export const DEFAULT_SEARCH: SearchState = { query: '', sort: 'Relevance', page: 1 };

function readSort(value: string | null): ListingSort {
  return SORT_OPTIONS.find((option) => option.value === value)?.value ?? DEFAULT_SEARCH.sort;
}

function readPage(value: string | null): number {
  const page = Number(value);

  // Clamped, not just validated: a hand-edited ?page=250 should show the last page the API will
  // serve rather than send a request it is certain to reject.
  return Number.isInteger(page) ? Math.min(Math.max(page, 1), MAX_PAGE) : DEFAULT_SEARCH.page;
}

/** Reads state from the URL, ignoring anything it does not recognise. */
export function readSearchState(params: URLSearchParams): SearchState {
  return {
    query: params.get('q')?.slice(0, MAX_QUERY_LENGTH) ?? DEFAULT_SEARCH.query,
    sort: readSort(params.get('sort')),
    page: readPage(params.get('page')),
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

  if (state.page !== DEFAULT_SEARCH.page) {
    params.set('page', String(state.page));
  }

  return params;
}

/** How many pages the UI may offer for a result set, within what the API will serve. */
export function pageCount(total: number): number {
  return Math.min(Math.ceil(total / PAGE_SIZE), MAX_PAGE);
}

export function toRequest(state: SearchState): ListingSearchRequest {
  return {
    query: state.query,
    sort: state.sort,
    page: state.page,
    pageSize: PAGE_SIZE,
  };
}
