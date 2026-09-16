import SearchIcon from '@mui/icons-material/Search';
import { Autocomplete, InputAdornment, TextField } from '@mui/material';
import { useDebounced } from '../../hooks/useDebounced';
import { MAX_QUERY_LENGTH } from './searchState';
import { MIN_SUGGEST_LENGTH, useSuggestions } from './useSuggestions';
import type { Suggestion } from '../../api/types';
import type { ReactElement } from 'react';

/** Shorter than the search's own debounce: a suggestion costs one cheap request, not a hybrid search. */
const SUGGEST_DEBOUNCE_MS = 120;

const GROUP_LABELS: Record<Suggestion['kind'], string> = {
  Neighborhood: 'Neighborhoods',
  Listing: 'Listings',
};

interface SearchBarProps {
  value: string;
  onChange: (value: string) => void;
  onPick: (suggestion: Suggestion) => void;
}

export function SearchBar({ value, onChange, onPick }: SearchBarProps): ReactElement {
  const settled = useDebounced(value, SUGGEST_DEBOUNCE_MS);
  const { data } = useSuggestions(settled);

  // Held-over suggestions for a prefix that is no longer there would be offered for a blank box.
  const options = value.trim().length >= MIN_SUGGEST_LENGTH ? data ?? [] : [];

  return (
    <Autocomplete<Suggestion, false, false, true>
      freeSolo
      fullWidth
      options={options}

      // A search box, not a select: the choice is applied and the box keeps the text it produced.
      value={null}
      inputValue={value}

      // The server already matched these, fuzzily and mid-word, which a client-side filter undoes.
      filterOptions={(candidates) => candidates}
      groupBy={(option) => GROUP_LABELS[option.kind]}
      getOptionLabel={(option) => (typeof option === 'string' ? option : option.text)}
      getOptionKey={(option) => (typeof option === 'string' ? option : `${option.kind}:${option.text}`)}

      // Not 'reset': after a choice it would write the neighbourhood back into a box pickSuggestion emptied.
      onInputChange={(_, next, reason) => {
        if (reason === 'input' || reason === 'clear') {
          onChange(next);
        }
      }}

      // A string here is Enter on free text, which the query already holds.
      onChange={(_, picked) => {
        if (picked !== null && typeof picked !== 'string') {
          onPick(picked);
        }
      }}
      renderInput={(params) => (
        <TextField
          {...params}
          placeholder="Sunny studio near Prospect Park"
          slotProps={{
            ...params.slotProps,

            // On the element, not on the TextField: the wrapper takes the attribute otherwise and
            // the input itself is left with a placeholder standing in for a label, which is not one.
            // maxLength stops at the same limit the API validates, so the box cannot produce a 400.
            htmlInput: { ...params.slotProps.htmlInput, 'aria-label': 'Search listings', maxLength: MAX_QUERY_LENGTH },
            input: {
              ...params.slotProps.input,
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon fontSize="small" />
                </InputAdornment>
              ),
              sx: { bgcolor: 'background.paper' },
            },
          }}
        />
      )}
    />
  );
}
