import { createTheme } from '@mui/material/styles';
import type { Theme } from '@mui/material/styles';

/** A calm, slightly warm palette: the listing photos are the colour in a real estate UI. */
export const theme: Theme = createTheme({
  palette: {
    primary: { main: '#1f6f5c' },
    secondary: { main: '#c2553d' },
    background: { default: '#f7f6f3', paper: '#ffffff' },
    text: { primary: '#1c1b19', secondary: '#5f5c57' },
  },
  shape: { borderRadius: 12 },
  typography: {
    fontFamily: '"Inter", system-ui, -apple-system, "Segoe UI", Roboto, sans-serif',
    h6: { fontWeight: 600 },
    subtitle1: { fontWeight: 600 },
  },
  components: {
    MuiCard: {
      defaultProps: { elevation: 0 },
      styleOverrides: { root: ({ theme }) => ({ border: `1px solid ${theme.palette.divider}` }) },
    },
    MuiChip: {
      defaultProps: { size: 'small' },
    },
  },
});
