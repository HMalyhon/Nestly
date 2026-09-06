import { Alert, AppBar, Box, Container, Pagination, Stack, Toolbar, Typography, useMediaQuery } from '@mui/material';
import { visuallyHidden } from '@mui/utils';
import { useRef } from 'react';
import { ResultsList } from './features/results/ResultsList';
import { SearchBar } from './features/search/SearchBar';
import { SortSelect } from './features/search/SortSelect';
import { pageCount, readSearchState, toRequest, writeSearchState } from './features/search/searchState';
import { useListingSearch } from './features/search/useListingSearch';
import { formatCount } from './format';
import { useDebounced } from './hooks/useDebounced';
import { useUrlState } from './hooks/useUrlState';
import type { ListingSort } from './api/types';
import type { ReactElement } from 'react';

const DEBOUNCE_MS = 200;

export function App(): ReactElement {
  const [params, writeParams] = useUrlState();
  const { query, sort, page } = readSearchState(params);

  // The URL is the only copy of the query -- the box reads it back, so there is no second state
  // to keep in step and the back button needs no special case. Only the request is debounced.
  const settled = useDebounced(query, DEBOUNCE_MS);

  // No useMemo: React Query hashes the key structurally, so a stable reference buys nothing.
  const { data, isPending, isError, error, isPlaceholderData } =
    useListingSearch(toRequest({ query: settled, sort, page }));

  const heading = useRef<HTMLHeadingElement>(null);
  const reducedMotion = useMediaQuery('(prefers-reduced-motion: reduce)');

  const type = (next: string): void => {
    writeParams(writeSearchState({ query: next, sort, page: 1 }), 'replace');
  };

  const move = (nextSort: ListingSort, nextPage: number): void => {
    writeParams(writeSearchState({ query, sort: nextSort, page: nextPage }), 'push');

    // Focus, not just scroll: paging with the keyboard would otherwise leave the focus ring on a
    // button that scrolling has pushed off-screen, with nothing announcing the new results.
    heading.current?.focus({ preventScroll: true });
    window.scrollTo({ top: 0, behavior: reducedMotion ? 'auto' : 'smooth' });
  };

  const pages = data ? pageCount(data.total) : 0;

  return (
    <>
      <AppBar position="sticky" color="inherit" elevation={0} sx={{ borderBottom: 1, borderColor: 'divider' }}>
        <Container maxWidth="lg">
          <Toolbar disableGutters sx={{ gap: 3, py: 1.5, flexWrap: 'wrap' }}>
            {/* A wordmark, not the page's heading: on a search page the heading describes the
                search, and it lives in <main> below. */}
            <Typography variant="h6" component="div" sx={{ color: 'primary.main', letterSpacing: '-0.02em' }}>
              Nestly
            </Typography>
            <Box sx={{ flexGrow: 1, minWidth: 260 }}>
              <SearchBar value={query} onChange={type} />
            </Box>
          </Toolbar>
        </Container>
      </AppBar>

      <Container component="main" maxWidth="lg" sx={{ py: 3 }}>
        <Typography component="h1" ref={heading} tabIndex={-1} sx={visuallyHidden}>
          Apartment search
        </Typography>

        <Stack
          direction="row"
          sx={{ justifyContent: 'space-between', alignItems: 'center', gap: 2, mb: 2, flexWrap: 'wrap' }}
        >
          {/* Polite, not assertive: the count changes on every keystroke and should be read when
              the screen reader next pauses, not over what the user is typing. The timing is for
              the demo and is not worth reading out at all. */}
          <Typography variant="body2" aria-live="polite" aria-atomic sx={{ color: 'text.secondary' }}>
            {isPending && 'Searching…'}
            {data && `${formatCount(data.total)} listings found`}
            {data && <Box component="span" aria-hidden> · {data.elapsedMs} ms</Box>}
          </Typography>
          <SortSelect value={sort} onChange={(next) => { move(next, 1); }} />
        </Stack>

        {isError && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error.message}
          </Alert>
        )}

        <ResultsList
          data={data}
          isPending={isPending}
          isStale={isPlaceholderData}
          onFirstPage={() => { move(sort, 1); }}
        />

        {pages > 1 && (
          <Stack sx={{ alignItems: 'center', mt: 4 }}>
            <Pagination
              count={pages}
              page={page}
              onChange={(_, next) => { move(sort, next); }}
              color="primary"
              shape="rounded"
            />
          </Stack>
        )}
      </Container>
    </>
  );
}
