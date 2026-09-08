import { Box, Button, LinearProgress, Stack, Typography } from '@mui/material';
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

  reducedMotion: boolean;

  /** The listing the cursor or a map click is on. */
  highlightedId: string | undefined;

  onHover: (id: string | undefined) => void;

  /** The listing pinned on the map, set from either pane. */
  selectedId: string | undefined;

  onSelect: (id: string | undefined) => void;

  /** Set when the highlight came from the map, so the list should catch up to it. */
  scrollToId: string | undefined;
}

export function ResultsList({
  data, isPending, isStale, onFirstPage, reducedMotion, highlightedId, onHover, selectedId,
  onSelect, scrollToId,
}: ResultsListProps): ReactElement {
  const list = useRef<HTMLUListElement>(null);

  // A clicked pin is often not on the page of twenty being shown, so this is a best effort: bring
  // the card into view when it exists and do nothing when it does not.
  useEffect(() => {
    if (scrollToId === undefined) {
      return;
    }

    list.current
      // Escaped: an id with a quote in it would be a SyntaxError thrown inside an effect, which
      // with no error boundary above takes the whole tree down.
      ?.querySelector(`[data-listing="${CSS.escape(scrollToId)}"]`)
      ?.scrollIntoView({ block: 'nearest', behavior: reducedMotion ? 'auto' : 'smooth' });
  }, [scrollToId, reducedMotion]);

  // Two different empty screens, and conflating them is how a paging bug reads as "no results".
  const noMatches = data?.total === 0;
  const pastTheEnd = data !== undefined && data.total > 0 && data.hits.length === 0;

  const busy = isPending || isStale;

  return (
    <>
      {/* The only signal that a search is in flight once results are on screen. Dimming them was
          the old one, and no opacity that reads as dimmed keeps body text above 4.5:1. */}
      <LinearProgress
        aria-hidden
        sx={{ height: 2, mb: '-2px', visibility: busy ? 'visible' : 'hidden', borderRadius: 1 }}
      />

      <Box
        component="ul"
        ref={list}
        aria-label="Listings"

        // list-style: none drops list semantics in Safari, so VoiceOver stops announcing "list,
        // 20 items" and the position of each one -- which is the whole point of a results page.
        role="list"
        aria-busy={busy}
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' },
          gap: 2,
          listStyle: 'none',
          m: 0,
          p: 0,

          // These are the previous search's results while the next one is in flight. Only a
          // slight fade, with the progress bar above carrying the signal: at the 0.55 this used to
          // be, snippet text fell to 2.45:1.
          opacity: isStale ? 0.9 : 1,
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
              isSelected={hit.listing.id === selectedId}
              onHover={onHover}
              onSelect={onSelect}
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
