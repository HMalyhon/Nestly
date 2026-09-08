import { useQuery } from '@tanstack/react-query';
import { getListing } from '../../api/client';
import type { Listing } from '../../api/types';
import type { UseQueryResult } from '@tanstack/react-query';

/**
 * One listing in full, fetched only when the results do not already carry it.
 */
// A direct document read on the server, so it costs a fraction of a search -- and it is skipped
// entirely for the twenty listings on screen, which arrive complete with the search response.
export function useListing(id: string | undefined): UseQueryResult<Listing> {
  return useQuery({
    queryKey: ['listing', id],
    queryFn: ({ signal }) => getListing(id ?? '', signal),
    enabled: id !== undefined,

    // A listing does not change while someone is looking at it, and clicking back and forth
    // between two pins should not refetch either of them.
    staleTime: 5 * 60_000,
  });
}
