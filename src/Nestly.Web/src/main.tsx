import { CssBaseline, ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { ApiError } from './api/client';
import { App } from './App';
import { theme } from './theme/theme';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // A search box fires often enough already; refetching because the tab regained focus
      // would only add requests nobody asked for.
      refetchOnWindowFocus: false,

      // A rejected request is rejected for good: retrying a 400 only doubles the time the user
      // waits for an error that was never going to change.
      retry: (count, error) => count < 1 && error instanceof ApiError && error.isTransient,
    },
  },
});

const container = document.getElementById('root');

if (!container) {
  throw new Error('#root is missing from index.html.');
}

createRoot(container).render(
  <StrictMode>
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <QueryClientProvider client={queryClient}>
        <App />
      </QueryClientProvider>
    </ThemeProvider>
  </StrictMode>,
);
