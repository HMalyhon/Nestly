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

/** The set of bedroom counts one button stands for -- "5+" covers every larger bucket. */
function valuesFor(option: number, buckets: FacetBucket[]): number[] {
  if (option < OVERFLOW_FROM) {
    return [option];
  }

  return buckets.map((bucket) => Number(bucket.key)).filter((value) => value >= OVERFLOW_FROM);
}

export function BedroomsFacet({ buckets, selected, onChange }: BedroomsFacetProps): ReactElement {
  const options = [0, 1, 2, 3, 4, OVERFLOW_FROM];
  const chosen = options.filter((option) => valuesFor(option, buckets).some((value) => selected.includes(value)));

  const toggle = (option: number): void => {
    const values = valuesFor(option, buckets);
    const isOn = chosen.includes(option);

    onChange(isOn
      ? selected.filter((value) => !values.includes(value))
      : [...selected, ...values.filter((value) => !selected.includes(value))]);
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
