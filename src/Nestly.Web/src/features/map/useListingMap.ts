import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { mapListings } from '../../api/client';
import type { ListingMapRequest, MapResponse } from '../../api/types';
import type { UseQueryResult } from '@tanstack/react-query';

export function useListingMap(request: ListingMapRequest, enabled: boolean): UseQueryResult<MapResponse> {
  return useQuery({
    queryKey: ['listings-map', request],
    queryFn: ({ signal }) => mapListings(request, signal),

    // Markers stay put while the next viewport loads, so panning does not blink the map empty.
    placeholderData: keepPreviousData,
    staleTime: 30_000,
    enabled,
  });
}
