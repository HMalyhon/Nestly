import { MenuItem, TextField } from '@mui/material';
import { SORT_OPTIONS } from './searchState';
import type { ListingSort } from '../../api/types';
import type { ReactElement } from 'react';

interface SortSelectProps {
  value: ListingSort;
  onChange: (value: ListingSort) => void;
}

export function SortSelect({ value, onChange }: SortSelectProps): ReactElement {
  return (
    <TextField
      select
      size="small"
      label="Sort"
      value={value}
      onChange={(event) => { onChange(event.target.value as ListingSort); }}
      sx={{ minWidth: 190, bgcolor: 'background.paper' }}
    >
      {SORT_OPTIONS.map((option) => (
        <MenuItem key={option.value} value={option.value}>
          {option.label}
        </MenuItem>
      ))}
    </TextField>
  );
}
