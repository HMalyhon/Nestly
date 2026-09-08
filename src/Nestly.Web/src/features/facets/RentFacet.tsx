import { Box, Slider, Typography } from '@mui/material';
import { useState } from 'react';
import { formatMoney } from '../../format';
import type { FacetBucket } from '../../api/types';
import type { ReactElement } from 'react';

const STEP = 250;

type Range = [number, number];

interface RentFacetProps {
  histogram: FacetBucket[];
  bounds: Range;
  value: Range;
  onCommit: (value: Range) => void;
}

export function RentFacet({ histogram, bounds, value, onCommit }: RentFacetProps): ReactElement {
  const [draft, setDraft] = useState<Range>(value);
  const [applied, setApplied] = useState<Range>(value);

  // Adjusting state during render rather than in an effect: this is React's documented way to
  // follow a prop, and it is how "clear filters" and the back button reach the slider.
  if (value[0] !== applied[0] || value[1] !== applied[1]) {
    setApplied(value);
    setDraft(value);
  }

  const [min, max] = bounds;
  const tallest = Math.max(...histogram.map((bucket) => bucket.count), 1);

  return (
    <Box>
      <Typography component="p" variant="subtitle2" id="rent-facet">
        Monthly rent
      </Typography>
      <Typography variant="body2" sx={{ color: 'text.secondary', mb: 0.5 }}>
        {formatMoney(draft[0])} – {formatMoney(draft[1])}
      </Typography>

      {/* The distribution behind the track, so the long tail is visible rather than something you
          discover by dragging. Heights are square-rooted: linear, the tail buckets are a handful
          against a peak of ~575 and vanish. Decorative -- the slider carries the semantics. */}
      <Box aria-hidden sx={{ display: 'flex', alignItems: 'flex-end', gap: '1px', height: 44, mb: -1 }}>
        {histogram.map((bucket) => {
          const rent = Number(bucket.key);

          return (
            <Box
              key={bucket.key}
              sx={{
                flex: 1,
                minWidth: 0,
                height: `${String(Math.max(Math.sqrt(bucket.count / tallest) * 100, bucket.count > 0 ? 6 : 2))}%`,
                bgcolor: rent >= draft[0] && rent <= draft[1] ? 'primary.main' : 'action.disabledBackground',
                opacity: rent >= draft[0] && rent <= draft[1] ? 0.45 : 1,
                borderRadius: '1px 1px 0 0',
              }}
            />
          );
        })}
      </Box>

      <Slider
        size="small"
        value={draft}
        min={min}
        max={max}
        step={STEP}

        // No aria-labelledby beside these: MUI puts both on the same hidden input, where the
        // labelledby wins and both thumbs announce "Monthly rent".
        getAriaLabel={(index) => (index === 0 ? 'Minimum rent' : 'Maximum rent')}
        getAriaValueText={(rent) => formatMoney(rent)}
        valueLabelDisplay="off"
        onChange={(_, next) => { setDraft(next as Range); }}

        // On release, not on every pixel: dragging the handle would otherwise fire a search per
        // step, and the facet counts would churn under the cursor.
        onChangeCommitted={(_, next) => { onCommit(next as Range); }}
      />
    </Box>
  );
}
