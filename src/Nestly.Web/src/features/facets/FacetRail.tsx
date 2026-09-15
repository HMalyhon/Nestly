import { Box, Button, Chip, Divider, Stack, Typography } from '@mui/material';
import { LIST_FILTERS, countFilters } from '../search/searchState';
import { BedroomsFacet } from './BedroomsFacet';
import { CheckboxFacet } from './CheckboxFacet';
import { RentFacet } from './RentFacet';
import type { FacetBucket, ListingFacets, ListingFilters } from '../../api/types';
import type { ReactElement } from 'react';

const RENT_STEP = 250;

// What the rail falls back to when there is no response to count with. The filters live in the
// URL, not in the response, so a failed search still has to be undoable -- CheckboxFacet lists a
// selected value the buckets do not carry, which is exactly this case.
const NO_COUNTS: ListingFacets = {
  boroughs: [],
  neighborhoods: [],
  bedrooms: [],
  roomTypes: [],
  propertyTypes: [],
  amenities: [],
  rentHistogram: [],
};

/** The facets that are bucket lists, as opposed to the min/max rent numbers alongside them. */
type BucketFacet = {
  // -? because minRent and maxRent are optional, and an optional key would otherwise widen the
  // union with undefined rather than resolving to never.
  [K in keyof ListingFacets]-?: ListingFacets[K] extends FacetBucket[] ? K : never;
}[keyof ListingFacets];

const LISTS: { param: keyof typeof LIST_FILTERS; title: string; facet: BucketFacet }[] = [
  { param: 'borough', title: 'Borough', facet: 'boroughs' },
  { param: 'hood', title: 'Neighborhood', facet: 'neighborhoods' },
  { param: 'room', title: 'Room type', facet: 'roomTypes' },
  { param: 'type', title: 'Property type', facet: 'propertyTypes' },
  { param: 'amenity', title: 'Amenities', facet: 'amenities' },
];

interface FacetRailProps {
  facets: ListingFacets | undefined;
  filters: ListingFilters;
  onChange: (filters: ListingFilters) => void;
}

export function FacetRail({ facets, filters, onChange }: FacetRailProps): ReactElement | null {
  const active = countFilters(filters);

  // Before the first response there is genuinely nothing to draw; after a failed one there is
  // everything the user picked, and no other way to take it back.
  if (!facets && active === 0) {
    return null;
  }

  const counts = facets ?? NO_COUNTS;

  // Rounded outward to whole steps so the handles can actually reach both ends, and widened to
  // contain the active filter: another filter lifting the cheapest listing above the floor the
  // user set would otherwise put that floor off the track, where the next commit deletes it.
  const low = Math.floor(Math.min(counts.minRent ?? 0, filters.minRent ?? Infinity) / RENT_STEP) * RENT_STEP;
  const ceiling = Math.max(counts.maxRent ?? low + RENT_STEP, filters.maxRent ?? -Infinity);
  const high = Math.ceil(ceiling / RENT_STEP) * RENT_STEP;

  const commitRent = ([min, max]: [number, number]): void => {
    // A handle back at the end of the track is no filter at all, and should leave no trace in the
    // URL. The second arm keeps a handle the user did not touch: another filter can lift the
    // track's floor past a rent set earlier, and judging that rent by the new floor deleted it.
    const next: ListingFilters = { ...filters };

    delete next.minRent;
    delete next.maxRent;

    if (min > low || min === filters.minRent) {
      next.minRent = min;
    }

    if (max < high || max === filters.maxRent) {
      next.maxRent = max;
    }

    onChange(next);
  };

  return (
    <Stack component="section" aria-label="Filters" sx={{ gap: 2 }}>
      <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="subtitle1" component="h2">Filters</Typography>
        {active > 0 && (
          // Keeps the viewport: where the map is looking is not one of the choices being cleared.
          <Button size="small" onClick={() => { onChange(filters.within ? { within: filters.within } : {}); }}>
            Clear {active}
          </Button>
        )}
      </Stack>

      {/* The viewport narrows the results like any filter but is not one of them, so it is not in
          the count and "Clear" keeps it. Shown here because it is otherwise invisible -- below the
          map breakpoint there is no map on screen to explain why results are missing. */}
      {filters.within && (
        <Chip
          label="Map area"
          variant="outlined"
          onDelete={() => {
            const next = { ...filters };

            delete next.within;
            onChange(next);
          }}
          sx={{ alignSelf: 'flex-start' }}
        />
      )}

      {facets && high > low && (
        <>
          <RentFacet
            histogram={counts.rentHistogram}
            bounds={[low, high]}
            value={[filters.minRent ?? low, filters.maxRent ?? high]}
            onCommit={commitRent}
          />

          <Divider />
        </>
      )}

      <BedroomsFacet
        buckets={counts.bedrooms}
        selected={filters.bedrooms ?? []}
        onChange={(bedrooms) => { onChange({ ...filters, bedrooms }); }}
      />

      {LISTS.map(({ param, title, facet }) => {
        const selected = filters[LIST_FILTERS[param]] ?? [];

        // Skipped whole, divider included: the facet decides for itself whether it has anything
        // to show, and a rail of bare rules is what happens when only its contents are dropped.
        if (counts[facet].length === 0 && selected.length === 0) {
          return null;
        }

        return (
          <Box key={param}>
            <Divider sx={{ mb: 2 }} />
            <CheckboxFacet
              title={title}
              buckets={counts[facet]}
              selected={selected}
              onChange={(values) => { onChange({ ...filters, [LIST_FILTERS[param]]: values }); }}
            />
          </Box>
        );
      })}
    </Stack>
  );
}
