import { createContext, useCallback, useContext, useState, type ReactNode } from 'react';
import {
  Alert, Box, Button, Chip, CircularProgress, Paper, Snackbar, Stack, Table, TableBody, TableCell, TableContainer, TableHead,
  TablePagination, TableRow, Typography,
} from '@mui/material';
import { errorMessage } from '../api/client';
import { humanize } from '../api/types';

export function PageHeader({ title, subtitle, actions }: { title: string; subtitle?: string; actions?: ReactNode }) {
  return (
    <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" alignItems={{ sm: 'center' }} spacing={2} sx={{ mb: 3 }}>
      <Box>
        <Typography variant="h5" fontWeight={700}>{title}</Typography>
        {subtitle && <Typography color="text.secondary">{subtitle}</Typography>}
      </Box>
      {actions && <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>{actions}</Stack>}
    </Stack>
  );
}

export function Loading() {
  return <Box sx={{ display: 'flex', justifyContent: 'center', p: 6 }}><CircularProgress aria-label="Loading" /></Box>;
}

export function ErrorAlert({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  return (
    <Alert severity="error" action={onRetry && <Button color="inherit" size="small" onClick={onRetry}>Retry</Button>}>{errorMessage(error)}</Alert>
  );
}

const statusColors: Record<string, 'default' | 'success' | 'warning' | 'error' | 'info'> = {
  Published: 'success', Active: 'success', Resolved: 'success', Draft: 'warning', Open: 'warning', Pending: 'warning',
  Archived: 'default', Rejected: 'default', Expired: 'default', Cancelled: 'default', Revoked: 'error', GracePeriod: 'info',
};
export function StatusChip({ status }: { status: string }) {
  return <Chip size="small" label={humanize(status)} color={statusColors[status] ?? 'default'} variant="outlined" />;
}

export interface Column<T> { header: string; render: (row: T) => ReactNode; width?: number | string; align?: 'left' | 'right' | 'center' }

/** Data table with optional server-side pagination. */
export function DataTable<T>({ rows, columns, rowKey, loading, error, onRetry, empty = 'Nothing here yet.', page, onPage, onRowClick }: {
  rows: T[] | undefined; columns: Column<T>[]; rowKey: (r: T) => string; loading?: boolean; error?: unknown; onRetry?: () => void; empty?: string;
  page?: { page: number; pageSize: number; totalCount: number }; onPage?: (page: number, pageSize: number) => void; onRowClick?: (r: T) => void;
}) {
  if (error) return <ErrorAlert error={error} onRetry={onRetry} />;
  if (loading && !rows) return <Loading />;
  return (
    <Paper variant="outlined">
      <TableContainer>
        <Table size="small">
          <TableHead>
            <TableRow>{columns.map((c, i) => <TableCell key={i} align={c.align} sx={{ width: c.width, fontWeight: 600 }}>{c.header}</TableCell>)}</TableRow>
          </TableHead>
          <TableBody>
            {(rows ?? []).length === 0 ? (
              <TableRow><TableCell colSpan={columns.length}><Typography color="text.secondary" sx={{ py: 3, textAlign: 'center' }}>{empty}</Typography></TableCell></TableRow>
            ) : (rows ?? []).map((r) => (
              <TableRow key={rowKey(r)} hover onClick={onRowClick ? () => onRowClick(r) : undefined} sx={{ cursor: onRowClick ? 'pointer' : undefined }}>
                {columns.map((c, i) => <TableCell key={i} align={c.align}>{c.render(r)}</TableCell>)}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      {page && onPage && (
        <TablePagination component="div" count={page.totalCount} page={page.page - 1} rowsPerPage={page.pageSize} rowsPerPageOptions={[10, 20, 50, 100]}
          onPageChange={(_, p) => onPage(p + 1, page.pageSize)} onRowsPerPageChange={(e) => onPage(1, Number(e.target.value))} />
      )}
    </Paper>
  );
}

export function StatCard({ label, value, hint }: { label: string; value: ReactNode; hint?: string }) {
  return (
    <Paper variant="outlined" sx={{ p: 2, flex: '1 1 180px' }}>
      <Typography variant="body2" color="text.secondary">{label}</Typography>
      <Typography variant="h5" fontWeight={700}>{value}</Typography>
      {hint && <Typography variant="caption" color="text.secondary">{hint}</Typography>}
    </Paper>
  );
}

export const formatDate = (iso: string | null | undefined) => (iso ? new Date(iso).toLocaleString() : '—');

type Notify = (message: string, severity?: 'success' | 'error' | 'info') => void;
const NotifyContext = createContext<Notify>(() => undefined);

export function NotifyProvider({ children }: { children: ReactNode }) {
  const [msg, setMsg] = useState<{ text: string; severity: 'success' | 'error' | 'info' } | null>(null);
  const notify = useCallback<Notify>((text, severity = 'success') => setMsg({ text, severity }), []);
  return (
    <NotifyContext.Provider value={notify}>
      {children}
      <Snackbar open={!!msg} autoHideDuration={4000} onClose={() => setMsg(null)} anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}>
        {msg ? <Alert severity={msg.severity} onClose={() => setMsg(null)} variant="filled">{msg.text}</Alert> : undefined}
      </Snackbar>
    </NotifyContext.Provider>
  );
}

export const useNotify = () => useContext(NotifyContext);
