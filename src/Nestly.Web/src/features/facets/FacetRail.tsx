import { Box, Button, Divider, Stack, Typography } from '@mui/material';
import { LIST_FILTERS, countFilters } from '../search/searchState';
import { BedroomsFacet } from './BedroomsFacet';
import { CheckboxFacet } from './CheckboxFacet';
import { RentFacet } from './RentFacet';
import type { FacetBucket, ListingFacets, ListingFilters } from '../../api/types';
import type { ReactElement } from 'react';

const RENT_STEP = 250;

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
  if (!facets) {
    return null;
  }

  const active = countFilters(filters);

  // Rounded outward to whole steps so the handles can actually reach both ends.
  const low = Math.floor((facets.minRent ?? 0) / RENT_STEP) * RENT_STEP;
  const high = Math.ceil((facets.maxRent ?? low + RENT_STEP) / RENT_STEP) * RENT_STEP;

  const commitRent = ([min, max]: [number, number]): void => {
    // A range back at the ends is no filter at all, and should leave no trace in the URL.
    const next: ListingFilters = { ...filters };

    delete next.minRent;
    delete next.maxRent;

    if (min > low) {
      next.minRent = min;
    }

    if (max < high) {
      next.maxRent = max;
    }

    onChange(next);
  };

  return (
    <Stack component="section" aria-label="Filters" sx={{ gap: 2 }}>
      <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="subtitle1" component="h2">Filters</Typography>
        {active > 0 && (
          <Button size="small" onClick={() => { onChange({}); }}>
            Clear {active}
          </Button>
        )}
      </Stack>

      {high > low && (
        <RentFacet
          histogram={facets.rentHistogram}
          bounds={[low, high]}
          value={[filters.minRent ?? low, filters.maxRent ?? high]}
          onCommit={commitRent}
        />
      )}

      <Divider />

      <BedroomsFacet
        buckets={facets.bedrooms}
        selected={filters.bedrooms ?? []}
        onChange={(bedrooms) => { onChange({ ...filters, bedrooms }); }}
      />

      {LISTS.map(({ param, title, facet }) => (
        <Box key={param}>
          <Divider sx={{ mb: 2 }} />
          <CheckboxFacet
            title={title}
            buckets={facets[facet]}
            selected={filters[LIST_FILTERS[param]] ?? []}
            onChange={(values) => { onChange({ ...filters, [LIST_FILTERS[param]]: values }); }}
          />
        </Box>
      ))}
    </Stack>
  );
}
