import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { searchListings } from '../../api/client';
import type { ListingSearchRequest, ListingSearchResponse } from '../../api/types';
import type { UseQueryResult } from '@tanstack/react-query';

export function useListingSearch(request: ListingSearchRequest): UseQueryResult<ListingSearchResponse> {
  return useQuery({
    queryKey: ['listings', request],

    // The signal is React Query's: a keystroke that supersedes an in-flight request aborts it
    // rather than racing it.
    queryFn: ({ signal }) => searchListings(request, signal),

    // The list holds the previous page while the next one loads. Without it every keystroke
    // blanks the results, which is what "instant search" usually fails on.
    placeholderData: keepPreviousData,
    staleTime: 30_000,
  });
}
