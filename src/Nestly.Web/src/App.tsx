import FilterListIcon from '@mui/icons-material/FilterList';
import {
  Alert, AppBar, Badge, Box, Button, Container, Drawer, Pagination, Stack, ToggleButton,
  ToggleButtonGroup, Toolbar, Typography, useMediaQuery,
} from '@mui/material';
import { visuallyHidden } from '@mui/utils';
import { useQueryClient } from '@tanstack/react-query';
import { Suspense, lazy, useEffect, useRef, useState } from 'react';
import { FacetRail } from './features/facets/FacetRail';
import { useListingMap } from './features/map/useListingMap';
import { ResultsList } from './features/results/ResultsList';
import { useListing } from './features/results/useListing';
import { SearchBar } from './features/search/SearchBar';
import { SortSelect } from './features/search/SortSelect';
import {
  countFilters, pageCount, readSearchState, toMapRequest, toRequest, writeSearchState,
} from './features/search/searchState';
import { useListingSearch } from './features/search/useListingSearch';
import { formatCount, formatListings } from './format';
import { useDebounced } from './hooks/useDebounced';
import { useUrlState } from './hooks/useUrlState';
import type { GeoBounds, ListingFilters, ListingSearchResponse, ListingSort } from './api/types';
import type { ViewCause } from './features/map/MapPane';
import type { ReactElement } from 'react';

// Split out of the initial bundle: Leaflet and its CSS are a third of the download, and below the
// lg breakpoint the map is not rendered at all until the user asks for it.
const MapPane = lazy(async () => ({ default: (await import('./features/map/MapPane')).MapPane }));

const DEBOUNCE_MS = 200;
const RAIL_WIDTH = 272;

/** Sticky panes stop below the app bar and leave the page margin visible. */
const PANE_HEIGHT = 'calc(100vh - 148px)';

/**
 * What the live region says.
 */
// The visible count cannot carry it: "396 listings found" is identical from one page to the next,
// so a page turn mutated nothing and a screen reader announced nothing.
function describeResults(
  isPending: boolean,
  isError: boolean,
  data: ListingSearchResponse | undefined,
  page: number,
  pages: number,
): string {
  if (isPending) {
    return 'Searching…';
  }

  if (isError) {
    return 'The search failed.';
  }

  if (!data) {
    return '';
  }

  if (data.total === 0) {
    return 'Nothing matched that search.';
  }

  if (data.hits.length === 0) {
    return `Page ${String(page)} is past the end of ${formatCount(data.total)} results.`;
  }

  return `${formatListings(data.total)} found. Page ${String(page)} of ${String(pages)}.`;
}

export function App(): ReactElement {
  const queryClient = useQueryClient();
  const [params, writeParams] = useUrlState();
  const state = readSearchState(params);
  const { query, sort, page, filters } = state;

  // The URL is the only copy of the search -- the controls read it back, so there is no second
  // state to keep in step and the back button needs no special case. Only the request is debounced.
  const settled = useDebounced(query, DEBOUNCE_MS);

  const results = useRef<HTMLHeadingElement>(null);
  const reducedMotion = useMediaQuery('(prefers-reduced-motion: reduce)');
  const wideEnoughForRail = useMediaQuery((theme) => theme.breakpoints.up('md'));
  const wideEnoughForMap = useMediaQuery((theme) => theme.breakpoints.up('lg'));

  const [drawerOpen, setDrawerOpen] = useState(false);
  const [pane, setPane] = useState<'list' | 'map'>('list');
  const [hoveredId, setHoveredId] = useState<string>();
  const [selectedId, setSelectedId] = useState<string>();

  const mapVisible = wideEnoughForMap || pane === 'map';

  // No useMemo: React Query hashes the key structurally, so a stable reference buys nothing.
  const { data, isPending, isError, error, isPlaceholderData } =
    useListingSearch(toRequest({ ...state, query: settled }));

  // Only while the map is on screen -- there is no point fetching markers nobody can see.
  const map = useListingMap(toMapRequest({ ...state, query: settled }), mapVisible);

  const write = (next: Partial<typeof state>, mode: 'push' | 'replace'): void => {
    writeParams(writeSearchState({ ...state, ...next }, params), mode);
  };

  const jumpToTop = (): void => {
    // Focus, not just scroll: paging with the keyboard would otherwise leave the focus ring on a
    // button that scrolling has pushed off-screen, with nothing announcing the new results.
    results.current?.focus({ preventScroll: true });
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
  const look = (within: GeoBounds, nextZoom: number, cause: ViewCause): void => {
    // A pan narrows the results, so the page you were on may no longer exist. The mount report is
    // not a user action -- resetting on it discarded the page in every shared ?page=3 link.
    const next = { filters: { ...filters, within }, zoom: nextZoom };

    write(cause === 'pan' ? { ...next, page: 1 } : next, 'replace');
  };

  // Toggling, so clicking the same card or pin again clears it. Nothing else ever could: the
  // selection had no off switch at all.
  const select = (id: string | undefined): void => {
    setSelectedId((current) => (current === id ? undefined : id));
  };

  // The search returns each listing in full, so the detail cache is filled from it rather than
  // refetched: clicking a card costs nothing, and only a pin whose listing is off the current page
  // reaches the server at all.
  useEffect(() => {
    for (const hit of data?.hits ?? []) {
      queryClient.setQueryData(['listing', hit.listing.id], hit.listing);
    }
  }, [data, queryClient]);

  const detail = useListing(selectedId);

  const pages = data ? pageCount(data.total) : 0;
  const activeFilters = countFilters(filters);
  const rail = <FacetRail facets={data?.facets} filters={filters} onChange={filter} />;

  const mapPane = (
    <Suspense fallback={<Box sx={{ height: '100%', bgcolor: 'action.hover', borderRadius: 1 }} />}>
      <MapPane
        data={map.data}
        initialBounds={filters.within}
        onViewChange={look}

        // Read, not ignored: a failing map request used to leave the previous markers on screen,
        // or an empty city, with nothing anywhere saying why.
        error={map.isError ? map.error.message : undefined}
        highlightedId={hoveredId ?? selectedId}
        selected={detail.data}
        selectedId={selectedId}
        isLoadingSelected={detail.isPending && selectedId !== undefined}
        selectedError={detail.isError ? detail.error.message : undefined}
        onSelect={select}
      />
    </Suspense>
  );

  // isPlaceholderData means the previous page is still on screen, so announcing the new page
  // number here would describe results nobody is looking at yet.
  const status = describeResults(isPending || isPlaceholderData, isError, data, page, pages);

  return (
    <>
      {/* Sighted keyboard users have no other way past the rail: the facets are 30-odd tab stops
          between the search box and the results. */}
      <Box
        component="a"
        href="#results"
        sx={{
          ...visuallyHidden,
          '&:focus': {
            clip: 'auto',
            clipPath: 'none',
            position: 'fixed',
            top: 8,
            left: 8,
            zIndex: 'tooltip',
            width: 'auto',
            height: 'auto',
            overflow: 'visible',
            p: 1.5,
            borderRadius: 1,
            bgcolor: 'background.paper',
            boxShadow: 3,
            color: 'primary.main',
          },
        }}
      >
        Skip to results
      </Box>

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

                // The badge is aria-hidden, so without this the button is announced as bare
                // "Filters" and the count of what is applied never reaches a screen reader.
                aria-label={activeFilters > 0 ? `Filters, ${String(activeFilters)} applied` : 'Filters'}
              >
                Filters
              </Button>
            )}
          </Toolbar>
        </Container>
      </AppBar>

      <Container component="main" maxWidth={false} sx={{ py: 3 }}>
        <Typography component="h1" sx={visuallyHidden}>
          Apartment search
        </Typography>

        <Box aria-live="polite" aria-atomic sx={visuallyHidden}>{status}</Box>

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
              {/* The results region's heading, and the focus target for paging and sorting. It is
                  visible, unlike the clipped h1 that used to take focus: a focus ring on a 1px
                  element is one nobody can see, and it sat above the rail, so the next Tab went
                  backwards into the filters instead of on to the results. */}
              <Typography
                variant="body2"
                component="h2"
                id="results"
                ref={results}
                tabIndex={-1}
                sx={{ color: 'text.secondary' }}
              >
                {isPending && 'Searching…'}
                {data && `${formatListings(data.total)} found`}
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
                    reducedMotion={reducedMotion}
                    highlightedId={hoveredId ?? selectedId}
                    onHover={setHoveredId}
                    selectedId={selectedId}
                    onSelect={select}
                    scrollToId={selectedId}
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
        // Announced as a bare "dialog" otherwise: the role and aria-modal are on the paper slot,
        // and the name has to go with them.
        slotProps={{ paper: { 'aria-label': 'Filters', sx: { width: RAIL_WIDTH + 48, p: 2 } } }}
      >
        {rail}
      </Drawer>
    </>
  );
}
