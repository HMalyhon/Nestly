import type { ListingMapRequest, ListingSearchRequest, ListingSearchResponse, MapResponse } from './types';

// Relative by default, so the app calls its own origin: Vite proxies /api in dev, nginx does it
// in Compose. Set VITE_API_BASE_URL only to point a local UI at an API somewhere else.
// Trailing slash trimmed: with one, every path would be requested as //api/listings/search.
const baseUrl: string = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');

// Deliberately shorter than the API's own 30s Elasticsearch timeout, so a request that is never
// coming back becomes an error with a retry rather than a spinner with no end. The p99 here is
// under a second; anything near this is already broken.
const TIMEOUT_MS = 15_000;

/** Status used when the request never reached the API at all. */
export const NO_RESPONSE = 0;

interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

// ASP.NET writes validation failures into `errors` and leaves `detail` unset, so reading detail
// alone reports "One or more validation errors occurred." and drops the only useful sentence.
function describe(problem: ProblemDetails | undefined, status: number): string {
  return problem?.detail
    ?? Object.values(problem?.errors ?? {}).flat()[0]
    ?? problem?.title
    ?? `The API returned ${String(status)}.`;
}

/** A request the API refused, or never answered. Carries the ProblemDetails body when there was one. */
export class ApiError extends Error {
  readonly status: number;

  constructor(status: number, problem?: ProblemDetails) {
    super(describe(problem, status));
    this.name = 'ApiError';
    this.status = status;
  }

  /** True when trying again could plausibly succeed: a timeout or a cluster that is down. */
  get isTransient(): boolean {
    return this.status === NO_RESPONSE || this.status >= 500;
  }
}

// A rejection that is the caller giving up, not the API failing. These reach the body reads below
// as well as the fetch itself, so they are checked in one place.
function rethrowIfCancelled(cause: unknown): void {
  if (cause instanceof DOMException && cause.name === 'TimeoutError') {
    throw new ApiError(NO_RESPONSE, { title: 'The server took too long to answer.' });
  }

  // React Query aborts superseded requests; that is a cancellation, not something to report.
  if (cause instanceof DOMException && cause.name === 'AbortError') {
    throw cause;
  }
}

// Every endpoint here is a POST with a JSON body, so the caller passes the body and its signal
// rather than a RequestInit. Headers stay this function's business: HeadersInit also covers arrays
// and Headers instances, neither of which merges into an object literal the way it looks like it does.
async function request<T>(path: string, body: unknown, signal: AbortSignal): Promise<T> {
  let response: Response;

  try {
    response = await fetch(`${baseUrl}${path}`, {
      method: 'POST',
      body: JSON.stringify(body),

      // React Query's signal cancels superseded requests; the timeout covers the case where the
      // API accepts the connection and then never answers, which nothing else here would catch.
      signal: AbortSignal.any([signal, AbortSignal.timeout(TIMEOUT_MS)]),
      headers: { 'Content-Type': 'application/json' },
    });
  } catch (cause) {
    rethrowIfCancelled(cause);

    // Otherwise the browser's own wording ("Failed to fetch") would reach the screen.
    throw new ApiError(NO_RESPONSE, { title: 'Could not reach the server.' });
  }

  if (!response.ok) {
    // An error body is a courtesy, not a guarantee -- a proxy in the way may send HTML.
    const problem = await response.json().catch((cause: unknown) => {
      rethrowIfCancelled(cause);

      return undefined;
    }) as ProblemDetails | undefined;

    throw new ApiError(response.status, problem);
  }

  try {
    return await response.json() as T;
  } catch (cause) {
    // The one place that used to trust the transport: a truncated or non-JSON 200 threw a raw
    // SyntaxError, which is not an ApiError, so it never retried and reached the screen verbatim.
    rethrowIfCancelled(cause);

    throw new ApiError(NO_RESPONSE, { title: 'The server sent a response that could not be read.' });
  }
}

export function searchListings(
  body: ListingSearchRequest,
  signal: AbortSignal,
): Promise<ListingSearchResponse> {
  return request('/api/listings/search', body, signal);
}

// A separate call, not a slice of the search: panning must not re-transfer descriptions and
// amenities for everything on screen.
export function mapListings(body: ListingMapRequest, signal: AbortSignal): Promise<MapResponse> {
  return request('/api/listings/map', body, signal);
}
