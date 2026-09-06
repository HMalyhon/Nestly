import SearchIcon from '@mui/icons-material/Search';
import { InputAdornment, TextField } from '@mui/material';
import { MAX_QUERY_LENGTH } from './searchState';
import type { ReactElement } from 'react';

interface SearchBarProps {
  value: string;
  onChange: (value: string) => void;
}

export function SearchBar({ value, onChange }: SearchBarProps): ReactElement {
  return (
    <TextField
      fullWidth
      value={value}
      onChange={(event) => { onChange(event.target.value); }}
      placeholder="Sunny studio near Prospect Park"
      slotProps={{
        // On the element, not on the TextField: the wrapper takes the attribute otherwise and
        // the input itself is left with a placeholder standing in for a label, which is not one.
        // maxLength stops at the same limit the API validates, so the box cannot produce a 400.
        htmlInput: { 'aria-label': 'Search listings', maxLength: MAX_QUERY_LENGTH },
        input: {
          startAdornment: (
            <InputAdornment position="start">
              <SearchIcon fontSize="small" />
            </InputAdornment>
          ),
          sx: { bgcolor: 'background.paper' },
        },
      }}
    />
  );
}
