import { Box, ToggleButton, ToggleButtonGroup, Typography } from '@mui/material';
import type { FacetBucket } from '../../api/types';
import type { ReactElement } from 'react';

/** Everything at or above this collapses into one "5+" button. */
const OVERFLOW_FROM = 5;

interface BedroomsFacetProps {
  buckets: FacetBucket[];
  selected: number[];
  onChange: (values: number[]) => void;
}

/** Whether a chosen bedroom count is one this button stands for -- "5+" covers every larger one. */
// Independent of the buckets, unlike the values a button turns on: a narrowed result set drops the
// large buckets, and expanding "5+" against those left it unable to clear the values it had set.
function covers(option: number, value: number): boolean {
  return option < OVERFLOW_FROM ? value === option : value >= OVERFLOW_FROM;
}

/** The bedroom counts a button turns on, which can only be ones the index actually holds. */
function valuesFor(option: number, buckets: FacetBucket[]): number[] {
  if (option < OVERFLOW_FROM) {
    return [option];
  }

  return buckets.map((bucket) => Number(bucket.key)).filter((value) => value >= OVERFLOW_FROM);
}

export function BedroomsFacet({ buckets, selected, onChange }: BedroomsFacetProps): ReactElement {
  const options = [0, 1, 2, 3, 4, OVERFLOW_FROM];
  const chosen = options.filter((option) => selected.some((value) => covers(option, value)));

  const toggle = (option: number): void => {
    if (chosen.includes(option)) {
      onChange(selected.filter((value) => !covers(option, value)));

      return;
    }

    const values = valuesFor(option, buckets);

    onChange([...selected, ...values.filter((value) => !selected.includes(value))]);
  };

  return (
    <Box>
      <Typography component="p" variant="subtitle2" sx={{ mb: 0.75 }} id="bedrooms-facet">
        Bedrooms
      </Typography>
      <ToggleButtonGroup size="small" value={chosen} aria-labelledby="bedrooms-facet" sx={{ flexWrap: 'wrap' }}>
        {options.map((option) => (
          <ToggleButton
            key={option}
            value={option}
            onClick={() => { toggle(option); }}
            sx={{ px: 1.25, textTransform: 'none' }}
          >
            {option === 0 ? 'Studio' : option === OVERFLOW_FROM ? '5+' : option}
          </ToggleButton>
        ))}
      </ToggleButtonGroup>
    </Box>
  );
}
