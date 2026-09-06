import type { ListingSearchRequest, ListingSearchResponse } from './types';

// Relative by default, so the app calls its own origin: Vite proxies /api in dev, nginx does it
// in Compose. Set VITE_API_BASE_URL only to point a local UI at an API somewhere else.
const baseUrl: string = import.meta.env.VITE_API_BASE_URL ?? '';

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

// Headers are this function's business, not the caller's: HeadersInit also covers arrays and
// Headers instances, neither of which merges into an object literal the way it looks like it does.
async function request<T>(path: string, init: Omit<RequestInit, 'headers'>): Promise<T> {
  let response: Response;

  try {
    response = await fetch(`${baseUrl}${path}`, {
      ...init,
      headers: { 'Content-Type': 'application/json' },
    });
  } catch (cause) {
    // React Query aborts superseded requests; that is a cancellation, not something to report.
    if (cause instanceof DOMException && cause.name === 'AbortError') {
      throw cause;
    }

    // Otherwise the browser's own wording ("Failed to fetch") would reach the screen.
    throw new ApiError(NO_RESPONSE, { title: 'Could not reach the server.' });
  }

  if (!response.ok) {
    // An error body is a courtesy, not a guarantee -- a proxy in the way may send HTML.
    const problem = await response.json().catch(() => undefined) as ProblemDetails | undefined;

    throw new ApiError(response.status, problem);
  }

  return await response.json() as T;
}

export function searchListings(
  body: ListingSearchRequest,
  signal: AbortSignal,
): Promise<ListingSearchResponse> {
  return request('/api/listings/search', { method: 'POST', body: JSON.stringify(body), signal });
}
