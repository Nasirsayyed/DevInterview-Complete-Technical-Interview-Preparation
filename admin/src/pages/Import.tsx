import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Alert, Box, Button, MenuItem, Paper, Stack, TextField, Typography } from '@mui/material';
import { api, errorMessage } from '../api/client';
import { useTechnologies } from '../api/hooks';
import type { ImportResult } from '../api/types';
import { DataTable, PageHeader, useNotify } from '../components/Common';

export default function ImportPage() {
  const techs = useTechnologies();
  const notify = useNotify();
  const qc = useQueryClient();
  const [file, setFile] = useState<File | null>(null);
  const [result, setResult] = useState<ImportResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [exportTech, setExportTech] = useState('');

  const run = async (dryRun: boolean) => {
    if (!file) return;
    setBusy(true); setError(null);
    try {
      const r = await api.upload<ImportResult>('/admin/questions/import', file, { dryRun });
      setResult(r);
      if (!dryRun) { notify(`Imported: ${r.created} created, ${r.updated} updated`); await qc.invalidateQueries({ queryKey: ['admin'] }); }
    } catch (e) { setError(errorMessage(e)); } finally { setBusy(false); }
  };
  const download = (path: string, query: Record<string, string>, name: string) =>
    api.download(path, query, name).catch((e) => notify(errorMessage(e), 'error'));

  return (
    <>
      <PageHeader title="Import / export questions" subtitle="Bulk-load questions from CSV or Excel. Validate with a dry run first." />
      <Stack spacing={3}>
        <Paper variant="outlined" sx={{ p: 3 }}>
          <Typography variant="h6" gutterBottom>1. Download the template</Typography>
          <Typography color="text.secondary" sx={{ mb: 2 }}>
            Columns include technology, category, type, difficulty, options (with correct flags), answer and explanation. Rows with an
            external key update the existing question instead of creating a duplicate.
          </Typography>
          <Stack direction="row" spacing={1}>
            <Button variant="outlined" onClick={() => download('/admin/questions/import-template', { format: 'csv' }, 'question-import-template.csv')}>CSV template</Button>
            <Button variant="outlined" onClick={() => download('/admin/questions/import-template', { format: 'xlsx' }, 'question-import-template.xlsx')}>Excel template</Button>
          </Stack>
        </Paper>
        <Paper variant="outlined" sx={{ p: 3 }}>
          <Typography variant="h6" gutterBottom>2. Upload</Typography>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} alignItems={{ sm: 'center' }}>
            <Button variant="outlined" component="label">
              Choose file
              <input hidden type="file" accept=".csv,.xlsx" data-testid="import-file" onChange={(e) => { setFile(e.target.files?.[0] ?? null); setResult(null); }} />
            </Button>
            <Typography>{file ? `${file.name} (${Math.ceil(file.size / 1024)} KB)` : 'No file selected (max 10 MB)'}</Typography>
            <Box sx={{ flex: 1 }} />
            <Button onClick={() => run(true)} disabled={!file || busy}>Validate (dry run)</Button>
            <Button variant="contained" onClick={() => run(false)} disabled={!file || busy || !result?.dryRun || result.errors.length > 0}>Import</Button>
          </Stack>
          {error && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}
          {result && (
            <Box sx={{ mt: 2 }}>
              <Alert severity={result.errors.length ? 'warning' : 'success'} sx={{ mb: 2 }}>
                {result.dryRun ? 'Dry run: nothing was saved. ' : 'Import finished. '}
                {result.created} to create · {result.updated} to update · {result.skipped} skipped · {result.errors.length} error(s)
                {result.dryRun && result.errors.length > 0 && ' — fix the errors and validate again before importing.'}
              </Alert>
              {result.errors.length > 0 && (
                <DataTable rows={result.errors} rowKey={(e) => `${e.row}-${e.message}`} columns={[
                  { header: 'Row', render: (e) => e.row, width: 80 },
                  { header: 'Problem', render: (e) => e.message },
                ]} />
              )}
            </Box>
          )}
        </Paper>
        <Paper variant="outlined" sx={{ p: 3 }}>
          <Typography variant="h6" gutterBottom>Export</Typography>
          <Stack direction="row" spacing={2} alignItems="center">
            <TextField select size="small" label="Technology" value={exportTech} onChange={(e) => setExportTech(e.target.value)} sx={{ minWidth: 220 }}>
              <MenuItem value="">All technologies</MenuItem>
              {techs.data?.map((t) => <MenuItem key={t.id} value={t.id}>{t.name}</MenuItem>)}
            </TextField>
            <Button variant="outlined" onClick={() => download('/admin/questions/export', { format: 'csv', ...(exportTech ? { technologyId: exportTech } : {}) }, 'devinterview-questions.csv')}>Export CSV</Button>
            <Button variant="outlined" onClick={() => download('/admin/questions/export', { format: 'xlsx', ...(exportTech ? { technologyId: exportTech } : {}) }, 'devinterview-questions.xlsx')}>Export Excel</Button>
          </Stack>
        </Paper>
      </Stack>
    </>
  );
}
