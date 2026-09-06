import FilterListIcon from '@mui/icons-material/FilterList';
import {
  Alert, AppBar, Badge, Box, Button, Container, Drawer, Pagination, Stack, Toolbar, Typography, useMediaQuery,
} from '@mui/material';
import { visuallyHidden } from '@mui/utils';
import { useRef, useState } from 'react';
import { FacetRail } from './features/facets/FacetRail';
import { ResultsList } from './features/results/ResultsList';
import { SearchBar } from './features/search/SearchBar';
import { SortSelect } from './features/search/SortSelect';
import { countFilters, pageCount, readSearchState, toRequest, writeSearchState } from './features/search/searchState';
import { useListingSearch } from './features/search/useListingSearch';
import { formatCount } from './format';
import { useDebounced } from './hooks/useDebounced';
import { useUrlState } from './hooks/useUrlState';
import type { ListingFilters, ListingSort } from './api/types';
import type { ReactElement } from 'react';

const DEBOUNCE_MS = 200;
const RAIL_WIDTH = 272;

export function App(): ReactElement {
  const [params, writeParams] = useUrlState();
  const { query, sort, page, filters } = readSearchState(params);

  // The URL is the only copy of the search -- the controls read it back, so there is no second
  // state to keep in step and the back button needs no special case. Only the request is debounced.
  const settled = useDebounced(query, DEBOUNCE_MS);

  // No useMemo: React Query hashes the key structurally, so a stable reference buys nothing.
  const { data, isPending, isError, error, isPlaceholderData } =
    useListingSearch(toRequest({ query: settled, sort, page, filters }));

  const heading = useRef<HTMLHeadingElement>(null);
  const reducedMotion = useMediaQuery('(prefers-reduced-motion: reduce)');
  const wideEnoughForRail = useMediaQuery((theme) => theme.breakpoints.up('md'));
  const [drawerOpen, setDrawerOpen] = useState(false);

  const type = (next: string): void => {
    writeParams(writeSearchState({ query: next, sort, page: 1, filters }), 'replace');
  };

  const jumpToTop = (): void => {
    // Focus, not just scroll: paging with the keyboard would otherwise leave the focus ring on a
    // button that scrolling has pushed off-screen, with nothing announcing the new results.
    heading.current?.focus({ preventScroll: true });
    window.scrollTo({ top: 0, behavior: reducedMotion ? 'auto' : 'smooth' });
  };

  const move = (nextSort: ListingSort, nextPage: number): void => {
    writeParams(writeSearchState({ query, sort: nextSort, page: nextPage, filters }), 'push');
    jumpToTop();
  };

  // Narrowing the results invalidates the page you were on, so every filter change returns to 1.
  const filter = (next: ListingFilters): void => {
    writeParams(writeSearchState({ query, sort, page: 1, filters: next }), 'push');
  };

  const pages = data ? pageCount(data.total) : 0;
  const activeFilters = countFilters(filters);
  const rail = <FacetRail facets={data?.facets} filters={filters} onChange={filter} />;

  return (
    <>
      <AppBar position="sticky" color="inherit" elevation={0} sx={{ borderBottom: 1, borderColor: 'divider' }}>
        <Container maxWidth="xl">
          <Toolbar disableGutters sx={{ gap: 2, py: 1.5, flexWrap: 'wrap' }}>
            {/* A wordmark, not the page's heading: on a search page the heading describes the
                search, and it lives in <main> below. */}
            <Typography variant="h6" component="div" sx={{ color: 'primary.main', letterSpacing: '-0.02em' }}>
              Nestly
            </Typography>
            <Box sx={{ flexGrow: 1, minWidth: 240 }}>
              <SearchBar value={query} onChange={type} />
            </Box>
            {!wideEnoughForRail && (
              <Button
                startIcon={
                  <Badge badgeContent={activeFilters} color="primary">
                    <FilterListIcon />
                  </Badge>
                }
                onClick={() => { setDrawerOpen(true); }}
              >
                Filters
              </Button>
            )}
          </Toolbar>
        </Container>
      </AppBar>

      <Container component="main" maxWidth="xl" sx={{ py: 3 }}>
        <Typography component="h1" ref={heading} tabIndex={-1} sx={visuallyHidden}>
          Apartment search
        </Typography>

        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: { xs: '1fr', md: `${String(RAIL_WIDTH)}px minmax(0, 1fr)` },
            gap: 4,
          }}
        >
          {wideEnoughForRail && (
            <Box component="aside" sx={{ alignSelf: 'start', position: 'sticky', top: 96 }}>
              {rail}
            </Box>
          )}

          <Box sx={{ minWidth: 0 }}>
            <Stack
              direction="row"
              sx={{ justifyContent: 'space-between', alignItems: 'center', gap: 2, mb: 2, flexWrap: 'wrap' }}
            >
              {/* Polite, not assertive: the count changes on every keystroke and should be read
                  when the screen reader next pauses, not over what the user is typing. The timing
                  is for the demo and is not worth reading out at all. */}
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
          </Box>
        </Box>
      </Container>

      <Drawer
        open={drawerOpen && !wideEnoughForRail}
        onClose={() => { setDrawerOpen(false); }}
        slotProps={{ paper: { sx: { width: RAIL_WIDTH + 48, p: 2 } } }}
      >
        {rail}
      </Drawer>
    </>
  );
}
