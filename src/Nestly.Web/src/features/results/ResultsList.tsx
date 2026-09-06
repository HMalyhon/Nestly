import { Box, Button, Stack, Typography } from '@mui/material';
import { useEffect, useRef } from 'react';
import { formatCount } from '../../format';
import { ListingCard } from './ListingCard';
import { ResultsSkeleton } from './ResultsSkeleton';
import type { ListingSearchResponse } from '../../api/types';
import type { ReactElement } from 'react';

const SKELETON_COUNT = 6;

interface ResultsListProps {
  data: ListingSearchResponse | undefined;

  /** No results yet at all, as opposed to results that are merely out of date. */
  isPending: boolean;

  /** Showing the previous search while the next one is in flight. */
  isStale: boolean;

  onFirstPage: () => void;

  /** The listing the cursor or a map click is on. */
  highlightedId: string | undefined;

  onHover: (id: string | undefined) => void;

  /** Set when the highlight came from the map, so the list should catch up to it. */
  scrollToId: string | undefined;
}

export function ResultsList({
  data, isPending, isStale, onFirstPage, highlightedId, onHover, scrollToId,
}: ResultsListProps): ReactElement {
  const list = useRef<HTMLUListElement>(null);

  // A clicked pin is often not on the page of twenty being shown, so this is a best effort: bring
  // the card into view when it exists and do nothing when it does not.
  useEffect(() => {
    if (scrollToId === undefined) {
      return;
    }

    list.current
      ?.querySelector(`[data-listing="${scrollToId}"]`)
      ?.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
  }, [scrollToId]);

  // Two different empty screens, and conflating them is how a paging bug reads as "no results".
  const noMatches = data?.total === 0;
  const pastTheEnd = data !== undefined && data.total > 0 && data.hits.length === 0;

  return (
    <>
      <Box
        component="ul"
        ref={list}
        aria-label="Listings"
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' },
          gap: 2,
          listStyle: 'none',
          m: 0,
          p: 0,

          // These are the previous search's results while the next one is in flight; saying so
          // beats presenting stale cards as current.
          opacity: isStale ? 0.55 : 1,
          transition: 'opacity 120ms ease-out',
        }}
      >
        {isPending
          ? <ResultsSkeleton count={SKELETON_COUNT} />
          : data?.hits.map((hit) => (
            <ListingCard
              key={hit.listing.id}
              hit={hit}
              isHighlighted={hit.listing.id === highlightedId}
              onHover={onHover}
            />
          ))}
      </Box>

      {noMatches && (
        <Typography sx={{ color: 'text.secondary', py: 6, textAlign: 'center' }}>
          Nothing matched that. Try fewer words.
        </Typography>
      )}

      {pastTheEnd && (
        <Stack sx={{ alignItems: 'center', gap: 1, py: 6 }}>
          <Typography sx={{ color: 'text.secondary' }}>
            That page is past the end of these {formatCount(data.total)} results.
          </Typography>
          <Button onClick={onFirstPage}>Back to the first page</Button>
        </Stack>
      )}
    </>
  );
}
