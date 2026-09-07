import { Box, Button, Checkbox, FormControlLabel, Typography } from '@mui/material';
import { useState } from 'react';
import { formatCount } from '../../format';
import type { FacetBucket } from '../../api/types';
import type { ReactElement } from 'react';

const COLLAPSED_COUNT = 6;

interface CheckboxFacetProps {
  title: string;
  buckets: FacetBucket[];
  selected: string[];
  onChange: (values: string[]) => void;
}

export function CheckboxFacet({ title, buckets, selected, onChange }: CheckboxFacetProps): ReactElement | null {
  // Local because it is: how much of a list is unrolled is nobody's business but this component's,
  // and it has no place in a shared link.
  const [expanded, setExpanded] = useState(false);

  // Elasticsearch omits a term the current filters exclude rather than returning it at zero, so a
  // selected value can leave the response entirely. Listed from the selection, or it could be
  // applied and counted in the badge with no checkbox left to clear it.
  const missing = selected
    .filter((key) => !buckets.some((bucket) => bucket.key === key))
    .map((key) => ({ key, count: 0 }));
  const listed = [...buckets, ...missing];

  if (listed.length === 0) {
    return null;
  }

  const visible = expanded
    ? listed
    : [
      ...listed.slice(0, COLLAPSED_COUNT),
      // A ticked box below the fold applies a filter the collapsed list does not show, and the
      // rescued values above land at the end of the list precisely because they count zero.
      ...listed.slice(COLLAPSED_COUNT).filter((bucket) => selected.includes(bucket.key)),
    ];

  const toggle = (key: string): void => {
    onChange(selected.includes(key) ? selected.filter((value) => value !== key) : [...selected, key]);
  };

  return (
    <Box component="fieldset" sx={{ border: 0, p: 0, m: 0, minWidth: 0 }}>
      <Typography component="legend" variant="subtitle2" sx={{ mb: 0.5 }}>
        {title}
      </Typography>

      {visible.map((bucket) => (
        <FormControlLabel
          key={bucket.key}
          control={
            <Checkbox
              size="small"
              checked={selected.includes(bucket.key)}
              onChange={() => { toggle(bucket.key); }}
            />
          }
          sx={{ display: 'flex', mr: 0, '& .MuiFormControlLabel-label': { flexGrow: 1, minWidth: 0 } }}
          label={
            <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 1 }}>
              <Typography variant="body2" noWrap>{bucket.key}</Typography>
              <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                {formatCount(bucket.count)}
              </Typography>
            </Box>
          }
        />
      ))}

      {listed.length > COLLAPSED_COUNT && (
        <Button size="small" onClick={() => { setExpanded(!expanded); }} sx={{ mt: 0.5 }}>
          {expanded ? 'Show fewer' : `Show all ${String(listed.length)}`}
        </Button>
      )}
    </Box>
  );
}
