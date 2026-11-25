import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { CssBaseline, ThemeProvider, createTheme } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from './auth/AuthContext';
import { NotifyProvider } from './components/Common';
import App from './App';

const theme = createTheme({
  palette: { primary: { main: '#4F46E5' }, secondary: { main: '#0EA5E9' }, background: { default: '#F8FAFC' } },
  shape: { borderRadius: 10 },
  typography: { fontFamily: 'Inter, system-ui, -apple-system, Segoe UI, Roboto, sans-serif' },
  components: { MuiButton: { defaultProps: { disableElevation: true }, styleOverrides: { root: { textTransform: 'none', fontWeight: 600 } } } },
});

export const queryClient = new QueryClient({ defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: false, staleTime: 30_000 } } });

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <QueryClientProvider client={queryClient}>
        <NotifyProvider>
          <AuthProvider><App /></AuthProvider>
        </NotifyProvider>
      </QueryClientProvider>
    </ThemeProvider>
  </StrictMode>,
);
