import { createTheme } from '@mui/material/styles';
import type { Theme } from '@mui/material/styles';

/** A calm, slightly warm palette: the listing photos are the colour in a real estate UI. */
export const theme: Theme = createTheme({
  // MUI clears the browser's own outline on every ButtonBase and draws its replacement only when
  // this is set. Without it checkboxes, toggles, pagination and the slider had no focus ring at
  // all -- just a ripple, or a tint at 1.3:1.
  focusVisible: true,
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
    MuiCssBaseline: {
      // The app bar is sticky and opaque: without this, tabbing backwards to a control near the
      // top scrolls it underneath the bar.
      styleOverrides: { html: { scrollPaddingTop: 96 } },
    },
    MuiToggleButton: {
      styleOverrides: {
        // The default selected state is an 8% tint of the text colour, 1.18:1 against the button
        // next to it -- and it is the only thing marking a chosen bedroom count.
        root: ({ theme }) => ({
          '&.Mui-selected': {
            backgroundColor: theme.palette.primary.main,
            color: theme.palette.primary.contrastText,
            '&:hover': { backgroundColor: theme.palette.primary.dark },
          },
        }),
      },
    },
    MuiCard: {
      defaultProps: { elevation: 0 },
      styleOverrides: { root: ({ theme }) => ({ border: `1px solid ${theme.palette.divider}` }) },
    },
    MuiChip: {
      defaultProps: { size: 'small' },
    },
  },
});
