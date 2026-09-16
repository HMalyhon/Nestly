import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { suggestListings } from '../../api/client';
import type { Suggestion } from '../../api/types';
import type { UseQueryResult } from '@tanstack/react-query';

/** A single letter narrows nothing: "c" alone matches 23 of the 193 neighbourhoods. */
export const MIN_SUGGEST_LENGTH = 2;

export function useSuggestions(query: string): UseQueryResult<Suggestion[]> {
  const term = query.trim();

  return useQuery({
    queryKey: ['suggestions', term],
    queryFn: ({ signal }) => suggestListings(term, signal),
    enabled: term.length >= MIN_SUGGEST_LENGTH,

    // The list stays open on the last answer while the next prefix loads, rather than flickering.
    placeholderData: keepPreviousData,

    // What a prefix suggests does not change while someone is typing it.
    staleTime: 5 * 60_000,
  });
}
