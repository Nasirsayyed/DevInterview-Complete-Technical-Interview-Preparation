import { useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Box, MenuItem, Stack, Tab, Tabs, TextField, Typography } from '@mui/material';
import { api } from '../api/client';
import type { AiPrompt, AiProviderSetting, AiUsageSummary } from '../api/types';
import { DataTable, ErrorAlert, Loading, PageHeader, StatCard, StatusChip, formatDate, useNotify } from '../components/Common';
import { FormDialog } from '../components/Form';

export default function AiPage() {
  const [tab, setTab] = useState(0);
  return (
    <>
      <PageHeader title="AI" subtitle="Prompts, providers and usage. Provider API keys are configured on the server and are never shown here." />
      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2 }}><Tab label="Usage" /><Tab label="Prompts" /><Tab label="Providers" /></Tabs>
      {tab === 0 ? <Usage /> : tab === 1 ? <Prompts /> : <Providers />}
    </>
  );
}

function Usage() {
  const [days, setDays] = useState(30);
  const usage = useQuery({ queryKey: ['admin', 'ai-usage', days], queryFn: () => api.get<AiUsageSummary>('/admin/ai/usage', { days }) });
  if (usage.isLoading) return <Loading />;
  if (usage.error) return <ErrorAlert error={usage.error} onRetry={() => usage.refetch()} />;
  const u = usage.data!;
  return (
    <>
      <TextField select size="small" label="Period" value={days} onChange={(e) => setDays(Number(e.target.value))} sx={{ mb: 2, minWidth: 160 }}>
        {[1, 7, 30, 90].map((d) => <MenuItem key={d} value={d}>Last {d} day{d > 1 ? 's' : ''}</MenuItem>)}
      </TextField>
      <Stack direction="row" gap={2} flexWrap="wrap" sx={{ mb: 2 }}>
        <StatCard label="Calls" value={u.calls.toLocaleString()} hint={`${u.failedCalls} failed`} />
        <StatCard label="Prompt tokens" value={u.promptTokens.toLocaleString()} />
        <StatCard label="Completion tokens" value={u.completionTokens.toLocaleString()} />
        <StatCard label="Estimated cost" value={`$${Number(u.estimatedCostUsd).toFixed(2)}`} hint="Based on configured per-token prices" />
      </Stack>
      <DataTable rows={u.byFeature} rowKey={(f) => f.feature} empty="No AI calls in this period." columns={[
        { header: 'Feature', render: (f) => f.feature },
        { header: 'Calls', render: (f) => f.calls, align: 'right' },
        { header: 'Avg latency', render: (f) => `${f.averageLatencyMs} ms`, align: 'right' },
        { header: 'Est. cost', render: (f) => `$${Number(f.estimatedCostUsd).toFixed(2)}`, align: 'right' },
      ]} />
    </>
  );
}

type PromptForm = { key: string; systemPrompt: string; notes: string | null; isActive: boolean };

function Prompts() {
  const notify = useNotify();
  const qc = useQueryClient();
  const prompts = useQuery({ queryKey: ['admin', 'prompts'], queryFn: () => api.get<AiPrompt[]>('/admin/ai/prompts') });
  const [editing, setEditing] = useState<AiPrompt | null>(null);
  const initial = useMemo<PromptForm>(() => ({ key: editing?.key ?? '', systemPrompt: editing?.systemPrompt ?? '', notes: null, isActive: true }), [editing]);
  return (
    <>
      <Alert severity="info" sx={{ mb: 2 }}>
        Saving creates a new version and activates it; earlier versions are kept for audit. Prompts must still ask for the JSON shape the server parses —
        responses that do not validate are rejected and retried, never shown to learners.
      </Alert>
      <DataTable rows={prompts.data} loading={prompts.isLoading} error={prompts.error} onRetry={() => prompts.refetch()} rowKey={(p) => p.key} onRowClick={(p) => setEditing(p)}
        columns={[
          { header: 'Key', render: (p) => <Typography fontFamily="monospace" variant="body2">{p.key}</Typography> },
          { header: 'Version', render: (p) => (p.version === 0 ? 'Built-in' : p.version), align: 'right' },
          { header: 'Status', render: (p) => <StatusChip status={p.isActive ? 'Active' : 'Archived'} /> },
          { header: 'Notes', render: (p) => p.notes ?? '—' },
          { header: 'Updated', render: (p) => (p.version === 0 ? '—' : formatDate(p.updatedAt)) },
        ]} />
      <FormDialog<PromptForm> open={!!editing} title={`New version of ${editing?.key ?? ''}`} initial={initial} submitLabel="Save new version"
        fields={[
          { name: 'systemPrompt', label: 'System prompt', type: 'code', rows: 16, required: true },
          { name: 'notes', label: 'Change notes', nullable: true },
          { name: 'isActive', label: 'Activate this version', type: 'switch' },
        ]}
        onClose={() => setEditing(null)}
        onSubmit={async (v) => { await api.post('/admin/ai/prompts', v); notify('Prompt saved'); await qc.invalidateQueries({ queryKey: ['admin', 'prompts'] }); }} />
    </>
  );
}

function Providers() {
  const providers = useQuery({ queryKey: ['admin', 'providers'], queryFn: () => api.get<AiProviderSetting[]>('/admin/ai/providers') });
  return (
    <Box>
      <DataTable rows={providers.data} loading={providers.isLoading} error={providers.error} onRetry={() => providers.refetch()} rowKey={(p) => p.providerName}
        empty="No providers configured." columns={[
          { header: 'Provider', render: (p) => p.providerName },
          { header: 'Model', render: (p) => p.model },
          { header: 'Order', render: (p) => p.priority + 1, align: 'right' },
                    { header: 'API key', render: (p) => (p.apiKeyConfigured ? 'Configured on server' : 'Missing') },
          { header: 'Price / 1M tokens (in / out)', render: (p) => `$${p.inputCostPerMillionTokens} / $${p.outputCostPerMillionTokens}` },
        ]} />
      <Typography variant="caption" color="text.secondary">Provider order and models come from server configuration (AI:ProviderOrder, AI:Providers). Change them via environment settings and restart.</Typography>
    </Box>
  );
}

