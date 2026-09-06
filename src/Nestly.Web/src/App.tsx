import FilterListIcon from '@mui/icons-material/FilterList';
import {
  Alert, AppBar, Badge, Box, Button, Container, Drawer, Pagination, Stack, ToggleButton,
  ToggleButtonGroup, Toolbar, Typography, useMediaQuery,
} from '@mui/material';
import { visuallyHidden } from '@mui/utils';
import { useRef, useState } from 'react';
import { FacetRail } from './features/facets/FacetRail';
import { MapPane } from './features/map/MapPane';
import { useListingMap } from './features/map/useListingMap';
import { ResultsList } from './features/results/ResultsList';
import { SearchBar } from './features/search/SearchBar';
import { SortSelect } from './features/search/SortSelect';
import {
  countFilters, pageCount, readSearchState, toMapRequest, toRequest, writeSearchState,
} from './features/search/searchState';
import { useListingSearch } from './features/search/useListingSearch';
import { formatCount } from './format';
import { useDebounced } from './hooks/useDebounced';
import { useUrlState } from './hooks/useUrlState';
import type { GeoBounds, ListingFilters, ListingSort } from './api/types';
import type { ReactElement } from 'react';

const DEBOUNCE_MS = 200;
const RAIL_WIDTH = 272;

/** Sticky panes stop below the app bar and leave the page margin visible. */
const PANE_HEIGHT = 'calc(100vh - 148px)';

export function App(): ReactElement {
  const [params, writeParams] = useUrlState();
  const state = readSearchState(params);
  const { query, sort, page, filters } = state;

  // The URL is the only copy of the search -- the controls read it back, so there is no second
  // state to keep in step and the back button needs no special case. Only the request is debounced.
  const settled = useDebounced(query, DEBOUNCE_MS);

  const heading = useRef<HTMLHeadingElement>(null);
  const reducedMotion = useMediaQuery('(prefers-reduced-motion: reduce)');
  const wideEnoughForRail = useMediaQuery((theme) => theme.breakpoints.up('md'));
  const wideEnoughForMap = useMediaQuery((theme) => theme.breakpoints.up('lg'));

  const [drawerOpen, setDrawerOpen] = useState(false);
  const [pane, setPane] = useState<'list' | 'map'>('list');
  const [hoveredId, setHoveredId] = useState<string>();
  const [pinnedId, setPinnedId] = useState<string>();

  const mapVisible = wideEnoughForMap || pane === 'map';

  // No useMemo: React Query hashes the key structurally, so a stable reference buys nothing.
  const { data, isPending, isError, error, isPlaceholderData } =
    useListingSearch(toRequest({ ...state, query: settled }));

  // Only while the map is on screen -- there is no point fetching markers nobody can see.
  const map = useListingMap(toMapRequest({ ...state, query: settled }), mapVisible);

  const write = (next: Partial<typeof state>, mode: 'push' | 'replace'): void => {
    writeParams(writeSearchState({ ...state, ...next }), mode);
  };

  const jumpToTop = (): void => {
    // Focus, not just scroll: paging with the keyboard would otherwise leave the focus ring on a
    // button that scrolling has pushed off-screen, with nothing announcing the new results.
    heading.current?.focus({ preventScroll: true });
    window.scrollTo({ top: 0, behavior: reducedMotion ? 'auto' : 'smooth' });
  };

  const move = (nextSort: ListingSort, nextPage: number): void => {
    write({ sort: nextSort, page: nextPage }, 'push');
    jumpToTop();
  };

  // Narrowing the results invalidates the page you were on, so every filter change returns to 1.
  const filter = (next: ListingFilters): void => { write({ filters: next, page: 1 }, 'push'); };

  // Replace, not push: a pan is a continuous gesture, and a history entry per frame of it would
  // make the back button useless.
  const look = (within: GeoBounds, nextZoom: number): void => {
    write({ filters: { ...filters, within }, zoom: nextZoom, page: 1 }, 'replace');
  };

  const pages = data ? pageCount(data.total) : 0;
  const activeFilters = countFilters(filters);
  const rail = <FacetRail facets={data?.facets} filters={filters} onChange={filter} />;

  const mapPane = (
    <MapPane
      data={map.data}
      initialBounds={filters.within}
      onViewChange={look}
      highlightedId={hoveredId ?? pinnedId}
      onSelect={setPinnedId}
    />
  );

  return (
    <>
      <AppBar position="sticky" color="inherit" elevation={0} sx={{ borderBottom: 1, borderColor: 'divider' }}>
        <Container maxWidth={false}>
          <Toolbar disableGutters sx={{ gap: 2, py: 1.5, flexWrap: 'wrap' }}>
            {/* A wordmark, not the page's heading: on a search page the heading describes the
                search, and it lives in <main> below. */}
            <Typography variant="h6" component="div" sx={{ color: 'primary.main', letterSpacing: '-0.02em' }}>
              Nestly
            </Typography>
            <Box sx={{ flexGrow: 1, minWidth: 240 }}>
              <SearchBar value={query} onChange={(next) => { write({ query: next, page: 1 }, 'replace'); }} />
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

      <Container component="main" maxWidth={false} sx={{ py: 3 }}>
        <Typography component="h1" ref={heading} tabIndex={-1} sx={visuallyHidden}>
          Apartment search
        </Typography>

        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: {
              xs: '1fr',
              md: `${String(RAIL_WIDTH)}px minmax(0, 1fr)`,
              lg: `${String(RAIL_WIDTH)}px minmax(0, 1fr) minmax(360px, 0.85fr)`,
            },
            gap: 3,
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

              <Stack direction="row" sx={{ gap: 1, alignItems: 'center' }}>
                {!wideEnoughForMap && (
                  <ToggleButtonGroup
                    size="small"
                    exclusive
                    value={pane}
                    onChange={(_, next: 'list' | 'map' | null) => { setPane(next ?? pane); }}
                    aria-label="Result view"
                  >
                    <ToggleButton value="list" sx={{ textTransform: 'none' }}>List</ToggleButton>
                    <ToggleButton value="map" sx={{ textTransform: 'none' }}>Map</ToggleButton>
                  </ToggleButtonGroup>
                )}
                <SortSelect value={sort} onChange={(next) => { move(next, 1); }} />
              </Stack>
            </Stack>

            {isError && (
              <Alert severity="error" sx={{ mb: 2 }}>
                {error.message}
              </Alert>
            )}

            {mapVisible && !wideEnoughForMap
              ? <Box sx={{ height: PANE_HEIGHT }}>{mapPane}</Box>
              : (
                <>
                  <ResultsList
                    data={data}
                    isPending={isPending}
                    isStale={isPlaceholderData}
                    onFirstPage={() => { move(sort, 1); }}
                    highlightedId={hoveredId ?? pinnedId}
                    onHover={setHoveredId}
                    scrollToId={pinnedId}
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
                </>
              )}
          </Box>

          {wideEnoughForMap && (
            <Box sx={{ position: 'sticky', top: 96, height: PANE_HEIGHT }}>{mapPane}</Box>
          )}
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
